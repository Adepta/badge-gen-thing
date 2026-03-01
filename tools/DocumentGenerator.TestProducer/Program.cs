using DocumentGenerator.Messaging.Messages;
using DocumentGenerator.TestProducer.Configuration;
using DocumentGenerator.TestProducer.Infrastructure;
using DocumentGenerator.TestProducer.Messaging;
using DocumentGenerator.TestProducer.Worker;
using Rebus.Bus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Extensions.Logging;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Rebus.Config;
using Rebus.Kafka;
using Rebus.Routing.TypeBased;
using Rebus.ServiceProvider;

// ---------------------------------------------------------------------------
// Bootstrap NLog before anything else so startup failures are captured.
// ---------------------------------------------------------------------------
LogManager.Setup().LoadConfigurationFromFile("NLog.config");
var bootstrapLogger = LogManager.GetCurrentClassLogger();
bootstrapLogger.Info("DocumentGenerator.TestProducer starting up.");

try
{
    // -----------------------------------------------------------------------
    // Read configuration early — needed before the host is built so we can
    // decide which service-manager integration to register.
    // -----------------------------------------------------------------------
    var preConfig = new ConfigurationBuilder()
        .SetBasePath(AppContext.BaseDirectory)
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
        .AddJsonFile(
            $"appsettings.{Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production"}.json",
            optional: true, reloadOnChange: false)
        .AddEnvironmentVariables()
        .AddCommandLine(args)
        .Build();

    var kafkaBootstrap = preConfig["Kafka:BootstrapServers"] ?? "localhost:9092";
    var requestTopic   = preConfig["Kafka:RequestTopic"]     ?? "render.requests";
    var otelOptions    = preConfig.GetSection(OtelOptions.SectionName).Get<OtelOptions>() ?? new OtelOptions();

    var rawMode = preConfig.GetValue<string>("ServiceMode") ?? "Auto";
    if (!Enum.TryParse<ServiceMode>(rawMode, ignoreCase: true, out var serviceMode))
        serviceMode = ServiceMode.Auto;

    if (serviceMode == ServiceMode.Auto)
        serviceMode = HostingHelpers.DetectServiceMode();

    bootstrapLogger.Info("Service mode: {0} | Kafka: {1} | OTel enabled: {2}",
        serviceMode, kafkaBootstrap, otelOptions.Enabled);

    // -----------------------------------------------------------------------
    // Build resource descriptor (shared by tracing, metrics, and log bridge).
    // -----------------------------------------------------------------------
    var resource = ResourceBuilder.CreateDefault()
        .AddService(serviceName: otelOptions.ServiceName, serviceVersion: otelOptions.ServiceVersion)
        .AddAttributes(new Dictionary<string, object>
        {
            ["host.name"]      = Environment.MachineName,
            ["deployment.env"] = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production",
            ["service.mode"]   = serviceMode.ToString()
        });

    // -----------------------------------------------------------------------
    // Each producer instance gets a unique consumer-group so multiple instances
    // running in parallel do not steal each other's reply messages.
    // -----------------------------------------------------------------------
    var sessionId = Guid.NewGuid();

    // -----------------------------------------------------------------------
    // Host
    // -----------------------------------------------------------------------
    var builder = Host.CreateDefaultBuilder(args);

    // ── OS service-manager integration ──────────────────────────────────────
    switch (serviceMode)
    {
        case ServiceMode.WindowsService:
            builder.UseWindowsService(o => o.ServiceName = "DocGen.TestProducer");
            break;
        case ServiceMode.Systemd:
            builder.UseSystemd();
            break;
        // Console: no additional integration required.
    }

    builder
        // ── Configuration ────────────────────────────────────────────────────
        .ConfigureAppConfiguration((_, cfg) =>
        {
            cfg.Sources.Clear();
            cfg.SetBasePath(AppContext.BaseDirectory)
               .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
               .AddJsonFile(
                   $"appsettings.{Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production"}.json",
                   optional: true, reloadOnChange: true)
               .AddEnvironmentVariables()
               .AddCommandLine(args);
        })
        // ── Logging: NLog (structured sink) + OTel log bridge ────────────────
        .ConfigureLogging((_, logging) =>
        {
            logging
                .ClearProviders()
                // Trace level here; NLog.config governs the effective per-logger minimums.
                .SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace)
                .AddNLog(new NLogProviderOptions
                {
                    CaptureMessageTemplates  = true,
                    CaptureMessageProperties = true,
                    IgnoreEmptyEventId       = true,
                    ParseMessageTemplates    = true
                });

            if (otelOptions.Enabled)
            {
                logging.AddOpenTelemetry(otelLog =>
                {
                    otelLog.SetResourceBuilder(resource);
                    otelLog.IncludeFormattedMessage = true;
                    otelLog.IncludeScopes           = true;
                    otelLog.ParseStateValues        = true;

                    if (!string.IsNullOrWhiteSpace(otelOptions.OtlpEndpoint))
                        otelLog.AddOtlpExporter(o => o.Endpoint = new Uri(otelOptions.OtlpEndpoint));

                    if (otelOptions.ConsoleExporterEnabled)
                        otelLog.AddConsoleExporter();
                });
            }
        })
        // ── Services ─────────────────────────────────────────────────────────
        .ConfigureServices((ctx, services) =>
        {
            services
                .Configure<ProducerOptions>(ctx.Configuration.GetSection(ProducerOptions.SectionName))
                .Configure<OtelOptions>(ctx.Configuration.GetSection(OtelOptions.SectionName));

            // OpenTelemetry tracing + metrics
            if (otelOptions.Enabled)
            {
                services.AddOpenTelemetry()
                    .WithTracing(tracing =>
                    {
                        tracing.SetResourceBuilder(resource)
                               .AddSource("DocumentGenerator.TestProducer.*")
                               .AddHttpClientInstrumentation();

                        if (!string.IsNullOrWhiteSpace(otelOptions.OtlpEndpoint))
                            tracing.AddOtlpExporter(o => o.Endpoint = new Uri(otelOptions.OtlpEndpoint));

                        if (otelOptions.ConsoleExporterEnabled)
                            tracing.AddConsoleExporter();
                    })
                    .WithMetrics(metrics =>
                    {
                        metrics.SetResourceBuilder(resource)
                               .AddRuntimeInstrumentation()
                               .AddMeter("DocumentGenerator.TestProducer.*");

                        if (!string.IsNullOrWhiteSpace(otelOptions.OtlpEndpoint))
                            metrics.AddOtlpExporter(o => o.Endpoint = new Uri(otelOptions.OtlpEndpoint));

                        if (otelOptions.ConsoleExporterEnabled)
                            metrics.AddConsoleExporter();
                    });
            }

            // Thread-safe store that maps correlation IDs to awaiting tasks
            services.AddSingleton<ResultStore>();

            // Rebus + Kafka
            services.AddRebusHandler<ResultHandler>();
            services.AddRebus(
                configure => configure
                    .Transport(t => t.UseKafka(kafkaBootstrap, $"docgen-producer-{sessionId:N}"))
                    .Routing(r => r.TypeBased()
                        .Map<DocumentRenderRequest>(requestTopic)),
                onCreated: _ => Task.CompletedTask
            );

            // Subscribe after the host is fully started to avoid the deadlock
            // that occurs when bus.Subscribe is called inside onCreated.
            services.AddHostedService<ProducerResultSubscriptionService>();

            // Headless worker — replaces the old interactive menu
            services.AddHostedService<RenderJobWorker>();
        });

    await builder.Build().RunAsync();
}
catch (Exception ex) when (ex is not OperationCanceledException)
{
    bootstrapLogger.Fatal(ex, "Host terminated unexpectedly.");
    throw;
}
finally
{
    LogManager.Shutdown();
}

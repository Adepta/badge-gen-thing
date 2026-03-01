using DocumentGenerator.Console;
using DocumentGenerator.Console.Logging;
using DocumentGenerator.Core.Configuration;
using DocumentGenerator.Core.Interfaces;
using DocumentGenerator.Messaging.Configuration;
using DocumentGenerator.Messaging.Extensions;
using DocumentGenerator.Pdf.Extensions;
using DocumentGenerator.Templating.Extensions;
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
using PuppeteerSharp;
using Spectre.Console;

// ---------------------------------------------------------------------------
// Bootstrap NLog before the host so fatal startup errors are captured.
// ---------------------------------------------------------------------------
LogManager.Setup().LoadConfigurationFromFile("NLog.config");
var nlogBootstrap = LogManager.GetCurrentClassLogger();
nlogBootstrap.Info("DocumentGenerator.Console starting up.");

// ---------------------------------------------------------------------------
// Shared singletons created before the host so the Spectre TUI can start
// rendering immediately (the TuiRenderer hosted service reads from logBuffer).
// ---------------------------------------------------------------------------
var logBuffer   = new LogBuffer(capacity: 500);
var renderStats = new RenderStats();

AnsiConsole.Write(new FigletText("DocGenerator").Centered().Color(Color.Purple));
AnsiConsole.Write(new Rule("[grey]Document Render Service[/]").RuleStyle("grey").Centered());
AnsiConsole.WriteLine();

// Download / verify Chromium before starting the host.
await AnsiConsole.Progress()
    .AutoClear(false)
    .Columns(
        new TaskDescriptionColumn(),
        new ProgressBarColumn().FinishedStyle(Style.Parse("purple")),
        new SpinnerColumn(Spinner.Known.Dots).Style(Style.Parse("purple")))
    .StartAsync(async ctx =>
    {
        var task = ctx.AddTask("[grey]Checking Chromium...[/]", maxValue: 1);
        task.IsIndeterminate = true;
        await new BrowserFetcher().DownloadAsync();
        task.IsIndeterminate = false;
        task.Increment(1);
        task.Description = "[green]Chromium ready[/]";
    });

// ---------------------------------------------------------------------------
// Pin content root to the assembly directory so appsettings.json and
// templates/ are found correctly whether launched via `dotnet run` or a
// published binary (dotnet run sets CWD to the solution root).
// ---------------------------------------------------------------------------
var projectDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)!;

try
{
    var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
    {
        Args            = args,
        ContentRootPath = projectDir
    });

    // ── Read OTel config early (needed before logging is configured) ─────────
    var otelEnabled        = builder.Configuration.GetValue<bool>("OpenTelemetry:Enabled", defaultValue: true);
    var otelServiceName    = builder.Configuration.GetValue<string>("OpenTelemetry:ServiceName")    ?? "DocumentGenerator.Console";
    var otelServiceVersion = builder.Configuration.GetValue<string>("OpenTelemetry:ServiceVersion") ?? "1.0.0";
    var otelEndpoint       = builder.Configuration.GetValue<string>("OpenTelemetry:OtlpEndpoint")   ?? string.Empty;
    var otelConsole        = builder.Configuration.GetValue<bool>("OpenTelemetry:ConsoleExporterEnabled");

    // ── Shared OTel resource ─────────────────────────────────────────────────
    var resource = ResourceBuilder.CreateDefault()
        .AddService(serviceName: otelServiceName, serviceVersion: otelServiceVersion)
        .AddAttributes(new Dictionary<string, object>
        {
            ["host.name"]      = Environment.MachineName,
            ["deployment.env"] = builder.Environment.EnvironmentName
        });

    // ── Logging ──────────────────────────────────────────────────────────────
    // Three providers run side-by-side:
    //   1. SpectreConsole → LogBuffer → TuiRenderer (live TUI on the terminal)
    //   2. NLog           → rolling JSON-lines file under logs/
    //   3. OTel bridge    → OTLP collector → Loki
    //
    // ClearProviders() is NOT called here; the default providers are cleared
    // in CreateApplicationBuilder (it calls ClearProviders internally for the
    // "Worker" SDK). We add exactly the three we want.
    builder.Logging
        .ClearProviders()
        .SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace) // NLog.config + OTel govern effective per-logger minimums
        .AddSpectreConsole(logBuffer, Microsoft.Extensions.Logging.LogLevel.Debug)
        .AddNLog(new NLogProviderOptions
        {
            CaptureMessageTemplates  = true,
            CaptureMessageProperties = true,
            IgnoreEmptyEventId       = true,
            ParseMessageTemplates    = true
        });

    if (otelEnabled)
    {
        builder.Logging.AddOpenTelemetry(otelLog =>
        {
            otelLog.SetResourceBuilder(resource);
            otelLog.IncludeFormattedMessage = true;
            otelLog.IncludeScopes           = true;
            otelLog.ParseStateValues        = true;

            if (!string.IsNullOrWhiteSpace(otelEndpoint))
                otelLog.AddOtlpExporter(o => o.Endpoint = new Uri(otelEndpoint));

            if (otelConsole)
                otelLog.AddConsoleExporter();
        });
    }

    // ── OpenTelemetry tracing + metrics ──────────────────────────────────────
    if (otelEnabled)
    {
        builder.Services.AddOpenTelemetry()
            .WithTracing(tracing =>
            {
                tracing.SetResourceBuilder(resource)
                       .AddSource("DocumentGenerator.*")
                       .AddHttpClientInstrumentation();

                if (!string.IsNullOrWhiteSpace(otelEndpoint))
                    tracing.AddOtlpExporter(o => o.Endpoint = new Uri(otelEndpoint));

                if (otelConsole)
                    tracing.AddConsoleExporter();
            })
            .WithMetrics(metrics =>
            {
                metrics.SetResourceBuilder(resource)
                       .AddRuntimeInstrumentation()
                       .AddMeter("DocumentGenerator.*");

                if (!string.IsNullOrWhiteSpace(otelEndpoint))
                    metrics.AddOtlpExporter(o => o.Endpoint = new Uri(otelEndpoint));

                if (otelConsole)
                    metrics.AddConsoleExporter();
            });
    }

    // ── Application services ─────────────────────────────────────────────────
    builder.Services.AddSingleton(logBuffer);
    builder.Services.AddSingleton(renderStats);
    builder.Services.AddSingleton<IRenderMetrics>(renderStats);

    builder.Services
        .Configure<BrowserPoolOptions>(builder.Configuration.GetSection(BrowserPoolOptions.SectionName))
        .Configure<DocumentGeneratorOptions>(builder.Configuration.GetSection(DocumentGeneratorOptions.SectionName))
        .AddTemplating()
        .AddPdfRendering();

    // TuiRenderer owns the terminal — must start before any Kafka worker
    builder.Services.AddHostedService<TuiRenderer>();

    var kafkaEnabled = builder.Configuration.GetValue<bool>("Kafka:Enabled", defaultValue: false);

    if (kafkaEnabled)
    {
        var kafkaOptions = builder.Configuration
            .GetSection(KafkaOptions.SectionName)
            .Get<KafkaOptions>() ?? new KafkaOptions();

        builder.Services
            .Configure<KafkaOptions>(builder.Configuration.GetSection(KafkaOptions.SectionName))
            .AddRebusKafkaMessaging(kafkaOptions);

        logBuffer.Add(new LogBuffer.LogEntry(DateTime.Now, Microsoft.Extensions.Logging.LogLevel.Information, "Startup",
            $"Kafka consumer — {kafkaOptions.BootstrapServers}, {kafkaOptions.RequestTopic} → {kafkaOptions.ResultTopic}"));

        nlogBootstrap.Info("Kafka mode — BootstrapServers: {0}, RequestTopic: {1}",
            kafkaOptions.BootstrapServers, kafkaOptions.RequestTopic);
    }
    else
    {
        builder.Services.AddHostedService<DocumentGeneratorWorker>();

        logBuffer.Add(new LogBuffer.LogEntry(DateTime.Now, Microsoft.Extensions.Logging.LogLevel.Information, "Startup",
            "File worker — templates/ → output/"));

        nlogBootstrap.Info("File worker mode — scanning templates/");
    }

    await builder.Build().RunAsync();
}
catch (Exception ex) when (ex is not OperationCanceledException)
{
    nlogBootstrap.Fatal(ex, "Host terminated unexpectedly.");
    throw;
}
finally
{
    LogManager.Shutdown();
}

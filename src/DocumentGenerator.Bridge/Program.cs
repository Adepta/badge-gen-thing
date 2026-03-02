using DocumentGenerator.Bridge.Configuration;
using DocumentGenerator.Bridge.Endpoints;
using DocumentGenerator.Bridge.HealthChecks;
using DocumentGenerator.Bridge.Middleware;
using DocumentGenerator.Bridge.Printing;
using DocumentGenerator.Bridge.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Hosting.WindowsServices;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// ── Configuration binding ──────────────────────────────────────────────────────
builder.Services.Configure<BridgeOptions>(
    builder.Configuration.GetSection(BridgeOptions.SectionName));
builder.Services.Configure<CloudOptions>(
    builder.Configuration.GetSection(CloudOptions.SectionName));
builder.Services.Configure<PrinterOptions>(
    builder.Configuration.GetSection(PrinterOptions.SectionName));

// ── HTTP server port ───────────────────────────────────────────────────────────
var port = builder.Configuration.GetValue<int>($"{BridgeOptions.SectionName}:Port", 5100);
builder.WebHost.UseUrls($"http://+:{port}");

// ── Graceful shutdown ─────────────────────────────────────────────────────────
// Allow up to 30 seconds for in-flight print requests to complete on shutdown.
builder.Services.Configure<Microsoft.Extensions.Hosting.HostOptions>(o =>
    o.ShutdownTimeout = TimeSpan.FromSeconds(30));

// ── Windows Service / systemd lifetime support ─────────────────────────────────
if (WindowsServiceHelpers.IsWindowsService())
    builder.Host.UseWindowsService();
else
    builder.Host.UseSystemd();

// ── Printer adapter ────────────────────────────────────────────────────────────
builder.Services.AddPrinterAdapter(builder.Environment);

// ── Cloud HTTP client with circuit breaker ─────────────────────────────────────
// The client is configured lazily from options so setup changes take effect after restart.
// StandardResilienceHandler adds retry (3x), circuit breaker, and timeout automatically.
// The HttpClient sets BaseAddress and timeout. The API key is added per-request
// by CloudBadgeClient.RenderAsync (after decrypting ProtectedApiKey via DataProtection),
// so it is NOT set here — setting it here would bypass decryption.
builder.Services
    .AddHttpClient(CloudBadgeClient.HttpClientName, (sp, client) =>
    {
        var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<CloudOptions>>()
                     .CurrentValue;
        if (!string.IsNullOrWhiteSpace(opts.BaseUrl))
            client.BaseAddress = new Uri(opts.BaseUrl);
        client.Timeout = opts.Timeout + TimeSpan.FromSeconds(15); // outer timeout > resilience timeout
    })
    .AddStandardResilienceHandler(resilience =>
    {
        // Total timeout for the entire request including retries
        resilience.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(95);
        // Per-attempt timeout (the cloud render takes at most 30s)
        resilience.AttemptTimeout.Timeout      = TimeSpan.FromSeconds(35);
        // Retry up to 2 additional times on transient failures
        resilience.Retry.MaxRetryAttempts      = 2;
        // Circuit breaks after 50% failure rate over a 70s window (must be >= 2x AttemptTimeout of 35s)
        resilience.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(70);
    });

// ── Data Protection (for encrypting API key at rest) ──────────────────────────
builder.Services.AddDataProtection()
    .SetApplicationName("DocumentGenerator.Bridge");

builder.Services.AddSingleton<CloudBadgeClient>();
builder.Services.AddSingleton<SetupService>();

// ── CORS ────────────────────────────────────────────────────────────────────────
// In a production deployment the Bridge is accessed by iPads on the local LAN.
// Lock down to the configured origin(s) when possible. Wildcard is allowed when
// Bridge:AllowedOrigins is absent (development default).
var bridgeAllowedOrigins = builder.Configuration["Bridge:AllowedOrigins"];
builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
    {
        if (string.IsNullOrWhiteSpace(bridgeAllowedOrigins) || bridgeAllowedOrigins == "*")
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
        else
            policy.WithOrigins(bridgeAllowedOrigins.Split(',',
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                  .AllowAnyHeader()
                  .AllowAnyMethod();
    }));

// ── Health checks ──────────────────────────────────────────────────────────────
builder.Services.AddHealthChecks()
    .AddCheck<CloudConnectivityHealthCheck>("cloud-api");

// ── ProblemDetails (global error shape) ───────────────────────────────────────
builder.Services.AddProblemDetails();

// ── OpenTelemetry ──────────────────────────────────────────────────────────────
var otelEnabled     = builder.Configuration.GetValue<bool>("OpenTelemetry:Enabled", true);
var otelServiceName = builder.Configuration.GetValue<string>("OpenTelemetry:ServiceName")
                      ?? "DocumentGenerator.Bridge";
var otelVersion     = builder.Configuration.GetValue<string>("OpenTelemetry:ServiceVersion")
                      ?? "1.0.0";
var otelEndpoint    = builder.Configuration.GetValue<string>("OpenTelemetry:OtlpEndpoint")
                      ?? string.Empty;

if (otelEnabled)
{
    var resource = ResourceBuilder.CreateDefault()
        .AddService(otelServiceName, serviceVersion: otelVersion)
        .AddAttributes(new Dictionary<string, object>
        {
            ["host.name"]      = Environment.MachineName,
            ["deployment.env"] = builder.Environment.EnvironmentName
        });

    builder.Logging.AddOpenTelemetry(otelLog =>
    {
        otelLog.SetResourceBuilder(resource);
        otelLog.IncludeFormattedMessage = true;
        otelLog.IncludeScopes           = true;
        otelLog.ParseStateValues        = true;
        if (!string.IsNullOrWhiteSpace(otelEndpoint))
            otelLog.AddOtlpExporter(o => o.Endpoint = new Uri(otelEndpoint));
    });

    builder.Services.AddOpenTelemetry()
        .WithTracing(tracing =>
        {
            tracing.SetResourceBuilder(resource)
                   .AddSource("DocumentGenerator.*")
                   .AddAspNetCoreInstrumentation()
                   .AddHttpClientInstrumentation();
            if (!string.IsNullOrWhiteSpace(otelEndpoint))
                tracing.AddOtlpExporter(o => o.Endpoint = new Uri(otelEndpoint));
        })
        .WithMetrics(metrics =>
        {
            metrics.SetResourceBuilder(resource)
                   .AddRuntimeInstrumentation()
                   .AddAspNetCoreInstrumentation()
                   .AddMeter("DocumentGenerator.*");
            if (!string.IsNullOrWhiteSpace(otelEndpoint))
                metrics.AddOtlpExporter(o => o.Endpoint = new Uri(otelEndpoint));
        });
}

var app = builder.Build();

// ── Middleware ─────────────────────────────────────────────────────────────────
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors();

// Token-based authentication for non-setup, non-health endpoints.
// Disabled when Bridge:AccessToken is not configured (backward-compatible default).
app.UseMiddleware<BridgeTokenAuthMiddleware>();

// Redirect to setup wizard when bridge has not been configured
app.UseMiddleware<SetupGuardMiddleware>();

// ── Health endpoint ────────────────────────────────────────────────────────────
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    ResponseWriter = DocumentGenerator.Bridge.HealthChecks.HealthCheckResponseWriter.WriteJsonAsync
}).AllowAnonymous();

// ── Endpoints ──────────────────────────────────────────────────────────────────
app.MapSetupEndpoints();
app.MapBridgeEndpoints();

await app.RunAsync();

// Expose Program class for WebApplicationFactory in integration tests.
namespace DocumentGenerator.Bridge
{
    /// <summary>Exposes the entry-point class for <c>WebApplicationFactory&lt;Program&gt;</c> in integration tests.</summary>
    public partial class Program { }
}

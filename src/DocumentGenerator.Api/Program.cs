using System.Threading.RateLimiting;
using DocumentGenerator.Api.Authentication;
using DocumentGenerator.Api.Configuration;
using DocumentGenerator.Api.HealthChecks;
using DocumentGenerator.Api.Messaging;
using DocumentGenerator.Api.Services;
using DocumentGenerator.Messaging.Messages;
using DocumentGenerator.Pdf.Extensions;
using DocumentGenerator.Pdf.Pool;
using DocumentGenerator.Templating.Extensions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using PuppeteerSharp;
using Rebus.Config;
using Rebus.Kafka;
using Rebus.Routing.TypeBased;
using Rebus.ServiceProvider;

var builder = WebApplication.CreateBuilder(args);

// ── Kafka options (read early — drives whether Kafka path is active) ───────────
var kafkaOpts = builder.Configuration
    .GetSection(ApiKafkaOptions.SectionName)
    .Get<ApiKafkaOptions>() ?? new ApiKafkaOptions();

// ── Chromium download (inline mode only) ───────────────────────────────────────
if (!kafkaOpts.Enabled)
    await new BrowserFetcher().DownloadAsync();

// ── Controllers + OpenAPI ──────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// ── ProblemDetails (global error shape for unhandled exceptions) ───────────────
builder.Services.AddProblemDetails();

// ── API key authentication ─────────────────────────────────────────────────────
builder.Services
    .AddAuthentication(ApiKeyAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(
        ApiKeyAuthenticationHandler.SchemeName, _ => { });

builder.Services.AddAuthorization();

// ── Options validation at startup ─────────────────────────────────────────────
builder.Services
    .AddOptions<RateLimitOptions>()
    .Bind(builder.Configuration.GetSection(RateLimitOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// BrowserPool validation is handled by a custom IValidateOptions<> so we can
// produce human-readable messages beyond DataAnnotations.
builder.Services
    .AddOptions<DocumentGenerator.Core.Configuration.BrowserPoolOptions>()
    .Bind(builder.Configuration.GetSection(
        DocumentGenerator.Core.Configuration.BrowserPoolOptions.SectionName))
    .Validate(o => o.MaxSize >= 1,
        "BrowserPool:MaxSize must be at least 1.")
    .Validate(o => o.MinSize <= o.MaxSize,
        "BrowserPool:MinSize must be less than or equal to BrowserPool:MaxSize.")
    .ValidateOnStart();

builder.Services
    .AddOptions<ApiKafkaOptions>()
    .Bind(builder.Configuration.GetSection(ApiKafkaOptions.SectionName))
    .ValidateOnStart();

// ── Document pipeline ──────────────────────────────────────────────────────────
builder.Services.AddTemplating();
builder.Services.AddPdfRendering(pool =>
    builder.Configuration.GetSection("BrowserPool").Bind(pool));

// ── Template locator ───────────────────────────────────────────────────────────
builder.Services.AddSingleton<TemplateLocator>();

// ── Browser pool pre-warm (inline mode only) ───────────────────────────────────
// Launches MinSize Chromium instances at startup so the first render requests
// are served from warm browsers rather than paying the cold-start cost.
if (!kafkaOpts.Enabled)
    builder.Services.AddHostedService<BrowserPoolWarmUpService>();

// ── Kafka path (optional) ──────────────────────────────────────────────────────
if (kafkaOpts.Enabled)
{
    var instanceId    = Guid.NewGuid();
    var consumerGroup = $"api-{instanceId:N}";

    builder.Services.AddSingleton<PendingRenderStore>();
    builder.Services.AddRebusHandler<DocumentRenderResultHandler>();

    builder.Services.AddRebus(
        configure => configure
            .Transport(t => t.UseKafka(kafkaOpts.BootstrapServers, consumerGroup))
            .Routing(r => r.TypeBased()
                .Map<DocumentRenderRequest>(kafkaOpts.RequestTopic)),
        onCreated: _ => Task.CompletedTask
    );

    builder.Services.AddHostedService<ApiResultSubscriptionService>();
    builder.Services.AddHostedService<PendingRenderShutdownService>();
}

// ── Rate limiting — sliding window per IP ─────────────────────────────────────
builder.Services.AddRateLimiter(limiter =>
{
    var rateLimitSection = builder.Configuration.GetSection(RateLimitOptions.SectionName);
    var permitLimit      = rateLimitSection.GetValue<int>("PermitLimit",      10);
    var windowSeconds    = rateLimitSection.GetValue<int>("WindowSeconds",     60);
    var segments         = rateLimitSection.GetValue<int>("SegmentsPerWindow",  4);

    limiter.AddSlidingWindowLimiter(
        policyName: "render",
        options =>
        {
            options.PermitLimit         = permitLimit;
            options.Window              = TimeSpan.FromSeconds(windowSeconds);
            options.SegmentsPerWindow   = segments;
            options.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
            options.QueueLimit          = 0;
        });

    limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    limiter.OnRejected = async (ctx, ct) =>
    {
        ctx.HttpContext.Response.Headers["Retry-After"] =
            windowSeconds.ToString();
        await ctx.HttpContext.Response.WriteAsJsonAsync(
            new { error = "Too many requests. Please slow down.", retryAfterSeconds = windowSeconds },
            ct);
    };
});

// ── CORS ───────────────────────────────────────────────────────────────────────
var allowedOrigins = builder.Configuration["Cors:AllowedOrigins"] ?? "*";

builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
    {
        if (allowedOrigins == "*")
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
        else
            policy.WithOrigins(allowedOrigins.Split(',',
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                  .AllowAnyHeader()
                  .AllowAnyMethod();
    }));

// ── Request body size limit ────────────────────────────────────────────────────
// Default Kestrel limit is 30 MB — tighten to 1 MB for the render endpoint.
// Individual template variable payloads should never approach this.
builder.WebHost.ConfigureKestrel(k =>
    k.Limits.MaxRequestBodySize = 1 * 1024 * 1024); // 1 MB

// ── Graceful shutdown ─────────────────────────────────────────────────────────
// Allow up to 30 seconds for in-flight Chromium renders to complete on shutdown.
builder.Services.Configure<Microsoft.Extensions.Hosting.HostOptions>(o =>
    o.ShutdownTimeout = TimeSpan.FromSeconds(30));

// ── Health checks ──────────────────────────────────────────────────────────────
builder.Services.AddHealthChecks()
    .AddCheck<TemplateDirHealthCheck>("templates")
    .AddCheck<ChromiumPoolHealthCheck>("chromium");

// ── OpenTelemetry ──────────────────────────────────────────────────────────────
var otelEnabled     = builder.Configuration.GetValue<bool>("OpenTelemetry:Enabled", true);
var otelServiceName = builder.Configuration.GetValue<string>("OpenTelemetry:ServiceName")
                      ?? "DocumentGenerator.Api";
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

// ── Middleware pipeline ────────────────────────────────────────────────────────
// Global exception handler — returns RFC 9457 ProblemDetails on unhandled exceptions.
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

// HTTPS redirect — in Docker we terminate TLS at the load balancer/ingress so
// this only applies when TLS is bound directly (non-Docker deployments).
if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();

app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// Health endpoint — no auth, no rate limit
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    ResponseWriter = HealthCheckResponseWriter.WriteJsonAsync
}).AllowAnonymous();

app.MapControllers();

await app.RunAsync();

// Expose Program class for WebApplicationFactory in integration tests.
namespace DocumentGenerator.Api
{
    /// <summary>Exposes the entry-point class for <c>WebApplicationFactory&lt;Program&gt;</c> in integration tests.</summary>
    public partial class Program { }
}

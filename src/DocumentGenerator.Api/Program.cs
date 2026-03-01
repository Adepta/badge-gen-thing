using DocumentGenerator.Api.Authentication;
using DocumentGenerator.Api.Configuration;
using DocumentGenerator.Api.Messaging;
using DocumentGenerator.Api.Services;
using DocumentGenerator.Messaging.Messages;
using DocumentGenerator.Pdf.Extensions;
using DocumentGenerator.Templating.Extensions;
using Microsoft.AspNetCore.Authentication;
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
// In Kafka mode the Console owns the browser pool — the API never renders.
// Only download Chromium when the API will render in-process (Kafka disabled).
if (!kafkaOpts.Enabled)
    await new BrowserFetcher().DownloadAsync();

// ── Controllers ───────────────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// ── API key authentication ─────────────────────────────────────────────────────
builder.Services
    .AddAuthentication(ApiKeyAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(
        ApiKeyAuthenticationHandler.SchemeName, _ => { });

builder.Services.AddAuthorization();

// ── Document pipeline ──────────────────────────────────────────────────────────
// Always registered — used directly when Kafka is disabled, and kept available
// as a fallback / health-check even when Kafka is enabled.
builder.Services.AddTemplating();
builder.Services.AddPdfRendering(pool =>
    builder.Configuration.GetSection("BrowserPool").Bind(pool));

// ── Template locator ───────────────────────────────────────────────────────────
builder.Services.AddSingleton<TemplateLocator>();

// Always register ApiKafkaOptions so BadgesController can inject IOptions<ApiKafkaOptions>
// regardless of whether the Kafka path is active.
builder.Services.Configure<ApiKafkaOptions>(
    builder.Configuration.GetSection(ApiKafkaOptions.SectionName));

// ── Kafka path (optional) ──────────────────────────────────────────────────────
if (kafkaOpts.Enabled)
{
    // Unique consumer group per API instance ensures every result message is
    // delivered to this instance so it can resolve its own in-flight awaiters.
    var instanceId    = Guid.NewGuid();
    var consumerGroup = $"api-{instanceId:N}";

    // In-process store: CorrelationId → TaskCompletionSource
    builder.Services.AddSingleton<PendingRenderStore>();

    // Rebus handler that resolves awaiters when results arrive
    builder.Services.AddRebusHandler<DocumentRenderResultHandler>();

    builder.Services.AddRebus(
        configure => configure
            .Transport(t => t.UseKafka(kafkaOpts.BootstrapServers, consumerGroup))
            .Routing(r => r.TypeBased()
                .Map<DocumentRenderRequest>(kafkaOpts.RequestTopic)),
        onCreated: _ => Task.CompletedTask
    );

    // Subscribe after the host is fully started — calling Subscribe inside
    // onCreated deadlocks because Rebus hasn't finished initialising yet.
    builder.Services.AddHostedService<ApiResultSubscriptionService>();
    builder.Services.AddHostedService<PendingRenderShutdownService>();
}

// ── CORS ───────────────────────────────────────────────────────────────────────
// In production set Cors:AllowedOrigins to the specific Bridge origin(s).
// Wildcard (*) is acceptable in Development since the Bridge runs locally.
var allowedOrigins = builder.Configuration["Cors:AllowedOrigins"] ?? "*";

builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
    {
        if (allowedOrigins == "*")
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
        else
            policy.WithOrigins(allowedOrigins.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                  .AllowAnyHeader()
                  .AllowAnyMethod();
    }));

var app = builder.Build();

// ── Middleware pipeline ────────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

await app.RunAsync();

// Expose Program class for WebApplicationFactory in integration tests.
namespace DocumentGenerator.Api
{
    /// <summary>Exposes the entry-point class for <c>WebApplicationFactory&lt;Program&gt;</c> in integration tests.</summary>
    public partial class Program { }
}

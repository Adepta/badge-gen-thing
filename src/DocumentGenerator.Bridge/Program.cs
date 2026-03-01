using DocumentGenerator.Bridge.Configuration;
using DocumentGenerator.Bridge.Endpoints;
using DocumentGenerator.Bridge.Middleware;
using DocumentGenerator.Bridge.Printing;
using DocumentGenerator.Bridge.Services;
using Microsoft.Extensions.Hosting.WindowsServices;

var builder = WebApplication.CreateBuilder(args);

// ── Configuration binding ──────────────────────────────────────────────────────
builder.Services.Configure<BridgeOptions>(
    builder.Configuration.GetSection(BridgeOptions.Section));
builder.Services.Configure<CloudOptions>(
    builder.Configuration.GetSection(CloudOptions.Section));
builder.Services.Configure<PrinterOptions>(
    builder.Configuration.GetSection(PrinterOptions.Section));

// ── HTTP server port ───────────────────────────────────────────────────────────
var port = builder.Configuration.GetValue<int>($"{BridgeOptions.Section}:Port", 5100);
builder.WebHost.UseUrls($"http://+:{port}");

// ── Windows Service / systemd lifetime support ─────────────────────────────────
// Only activate the service-manager lifetime when actually running as a service.
// UseWindowsService() unconditionally replaces the console lifetime, which
// suppresses Ctrl+C when running interactively via `dotnet run`.
if (WindowsServiceHelpers.IsWindowsService())
    builder.Host.UseWindowsService();
else
    builder.Host.UseSystemd();

// ── Printer adapter ────────────────────────────────────────────────────────────
// Development: writes to Generated/ folder (no physical printer needed).
// Production:  OS-detected — WindowsPrinterAdapter or CupsPrinterAdapter.
builder.Services.AddPrinterAdapter(builder.Environment);

// ── Cloud HTTP client ──────────────────────────────────────────────────────────
// Configured lazily from options so setup changes take effect after restart.
builder.Services.AddHttpClient(CloudBadgeClient.HttpClientName, (sp, client) =>
{
    var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<CloudOptions>>()
                 .CurrentValue;
    if (!string.IsNullOrWhiteSpace(opts.BaseUrl))
        client.BaseAddress = new Uri(opts.BaseUrl);
    if (!string.IsNullOrWhiteSpace(opts.ApiKey))
        client.DefaultRequestHeaders.Add("X-Api-Key", opts.ApiKey);
    client.Timeout = opts.Timeout;
});

builder.Services.AddSingleton<CloudBadgeClient>();
builder.Services.AddSingleton<SetupService>();

// ── CORS — allow the iPad (any local origin) ───────────────────────────────────
builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod()));

var app = builder.Build();

// ── Middleware ─────────────────────────────────────────────────────────────────
app.UseCors();

// Redirect to setup wizard when bridge has not been configured
app.UseMiddleware<SetupGuardMiddleware>();

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

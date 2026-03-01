using DocumentGenerator.Bridge.Printing;
using DocumentGenerator.Bridge.Services;
using Microsoft.AspNetCore.Mvc;

namespace DocumentGenerator.Bridge.Endpoints;

/// <summary>
/// Maps the first-run setup wizard endpoints.
/// </summary>
/// <remarks>
/// When the bridge is unconfigured (<c>Bridge:IsConfigured = false</c>),
/// all non-health requests are redirected to <c>/setup</c> by the
/// <see cref="Middleware.SetupGuardMiddleware"/>.
///
/// The setup wizard is a single-page HTML form served from <c>/setup</c>.
/// On submission it posts to <c>/setup/save</c> which writes <c>appsettings.json</c>.
/// </remarks>
public static class SetupEndpoints
{
    /// <summary>
    /// Registers setup wizard routes on the given <see cref="WebApplication"/>.
    /// </summary>
    /// <param name="app">The application to register routes on.</param>
    public static void MapSetupEndpoints(this WebApplication app)
    {
        // Serve the HTML wizard page
        app.MapGet("/setup", async (HttpContext ctx) =>
        {
            var html = await File.ReadAllTextAsync(
                Path.Combine(AppContext.BaseDirectory, "wwwroot", "setup.html"));
            ctx.Response.ContentType = "text/html; charset=utf-8";
            await ctx.Response.WriteAsync(html);
        })
        .WithName("SetupPage")
        .WithSummary("Serves the first-run setup wizard HTML page.");

        // Test cloud connectivity (called via AJAX from the wizard)
        app.MapPost("/setup/test-connection", async (
            [FromBody] TestConnectionRequest req,
            SetupService setupService,
            CancellationToken ct) =>
        {
            var ok = await setupService.TestCloudConnectionAsync(req.BaseUrl, req.ApiKey, ct);
            return Results.Ok(new { connected = ok });
        })
        .WithName("TestConnection")
        .WithSummary("Tests connectivity to the cloud Badge Producer API.");

        // Get local printers (called via AJAX from the wizard)
        app.MapGet("/setup/printers", (IPrinterAdapter printerAdapter) =>
            Results.Ok(printerAdapter.GetAvailablePrinters()))
        .WithName("SetupGetPrinters")
        .WithSummary("Returns locally available printer names for setup wizard population.");

        // Save configuration and complete setup
        app.MapPost("/setup/save", async (
            [FromBody] SaveConfigRequest req,
            SetupService setupService,
            ILogger<WebApplication> logger,
            CancellationToken ct) =>
        {
            logger.LogInformation("Setup wizard saving configuration — BaseUrl={Url}", req.CloudBaseUrl);

            await setupService.SaveConfigurationAsync(
                req.CloudBaseUrl,
                req.ApiKey,
                req.DefaultPrinterName,
                req.Format,
                req.Port);

            return Results.Ok(new
            {
                success = true,
                message = "Configuration saved. Please restart the bridge service to apply changes."
            });
        })
        .WithName("SaveSetup")
        .WithSummary("Saves the setup wizard configuration and marks the bridge as configured.");
    }
}

/// <summary>Request body for the connection test endpoint.</summary>
public sealed class TestConnectionRequest
{
    /// <summary>Cloud API base URL to test.</summary>
    public string BaseUrl { get; init; } = string.Empty;
    /// <summary>API key to use in the test request.</summary>
    public string ApiKey { get; init; } = string.Empty;
}

/// <summary>Request body for saving the setup configuration.</summary>
public sealed class SaveConfigRequest
{
    /// <summary>Base URL of the cloud Badge Producer API.</summary>
    public string CloudBaseUrl { get; init; } = string.Empty;
    /// <summary>API key for authenticating with the cloud.</summary>
    public string ApiKey { get; init; } = string.Empty;
    /// <summary>Default local printer name; <c>null</c> for OS default.</summary>
    public string? DefaultPrinterName { get; init; }
    /// <summary>Document format: <c>"Pdf"</c> or <c>"Png"</c>.</summary>
    public string Format { get; init; } = "Pdf";
    /// <summary>Port the bridge HTTP server should listen on after restart.</summary>
    public int Port { get; init; } = 5100;
}

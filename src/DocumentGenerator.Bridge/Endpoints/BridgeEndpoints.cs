using System.Diagnostics;
using DocumentGenerator.Bridge.Configuration;
using DocumentGenerator.Bridge.Models;
using DocumentGenerator.Bridge.Printing;
using DocumentGenerator.Bridge.Services;
using DocumentGenerator.Core.Errors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace DocumentGenerator.Bridge.Endpoints;

/// <summary>
/// Maps all bridge HTTP endpoints using ASP.NET Core Minimal APIs.
/// </summary>
/// <remarks>
/// Endpoint summary:
/// <list type="table">
///   <listheader><term>Route</term><description>Purpose</description></listheader>
///   <item><term>GET  /health</term><description>Liveness probe — no auth required.</description></item>
///   <item><term>GET  /printers</term><description>Lists locally available printers.</description></item>
///   <item><term>GET  /templates</term><description>Proxies cloud template list.</description></item>
///   <item><term>POST /render</term><description>Render badge via cloud, return Base64 (no print).</description></item>
///   <item><term>POST /print</term><description>Render badge via cloud + send to local printer, return Base64.</description></item>
/// </list>
/// </remarks>
public static class BridgeEndpoints
{
    /// <summary>
    /// Registers all bridge routes on the given <see cref="WebApplication"/>.
    /// </summary>
    /// <param name="app">The application to register routes on.</param>
    public static void MapBridgeEndpoints(this WebApplication app)
    {
        // ── Health ────────────────────────────────────────────────────────────
        app.MapGet("/health", (IOptionsMonitor<BridgeOptions> opts) =>
            Results.Ok(new
            {
                status       = "healthy",
                isConfigured = opts.CurrentValue.IsConfigured,
                utc          = DateTimeOffset.UtcNow
            }))
            .WithName("Health")
            .WithSummary("Bridge liveness probe. No authentication required.");

        // ── Printers ──────────────────────────────────────────────────────────
        app.MapGet("/printers", (IPrinterAdapter printerAdapter) =>
        {
            var printers = printerAdapter.GetAvailablePrinters();
            return Results.Ok(printers);
        })
        .WithName("GetPrinters")
        .WithSummary("Returns a list of all locally available printer names.");

        // ── Templates (proxied from cloud) ────────────────────────────────────
        app.MapGet("/templates", async (
            CloudBadgeClient cloud,
            CancellationToken ct) =>
        {
            var templates = await cloud.ListTemplatesAsync(ct);
            return Results.Ok(templates);
        })
        .WithName("GetTemplates")
        .WithSummary("Returns badge template names available on the cloud API.");

        // ── Render (cloud only, no local print) ───────────────────────────────
        app.MapPost("/render", async (
            [FromBody] PrintRequest request,
            CloudBadgeClient cloud,
            IOptionsMonitor<PrinterOptions> printerOpts,
            ILogger<WebApplication> logger,
            CancellationToken ct) =>
        {
            var sw            = Stopwatch.StartNew();
            var correlationId = request.CorrelationId ?? Guid.NewGuid();
            var format        = printerOpts.CurrentValue.Format;

            logger.LogInformation(
                "Render request — CorrelationId={CorrelationId} Template={Template}",
                correlationId, request.TemplateName);

            try
            {
                var cloudResult = await cloud.RenderAsync(request, format, correlationId, ct);

                if (!cloudResult.Success || cloudResult.DocumentBase64 is null)
                    return Results.Ok(PrintResponse.Fail(correlationId, cloudResult.Error ?? "Cloud render failed.", sw.Elapsed));

                return Results.Ok(PrintResponse.RenderOk(
                    correlationId,
                    cloudResult.DocumentBase64,
                    cloudResult.MimeType ?? "application/pdf",
                    sw.Elapsed));
            }
            catch (PrintException ex)
            {
                logger.LogError(ex,
                    "[{ErrorCode}] Render failed — CorrelationId={CorrelationId}",
                    ex.ToString(), correlationId);
                return Results.Ok(PrintResponse.Fail(correlationId, ex.Message, sw.Elapsed, ex.ToString()));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Render failed — CorrelationId={CorrelationId}", correlationId);
                return Results.Ok(PrintResponse.Fail(correlationId, ex.Message, sw.Elapsed));
            }
        })
        .WithName("Render")
        .WithSummary("Renders a badge via the cloud API and returns Base64 bytes. Does not print locally.");

        // ── Print (cloud render + local print) ────────────────────────────────
        app.MapPost("/print", async (
            [FromBody] PrintRequest request,
            CloudBadgeClient cloud,
            IPrinterAdapter printerAdapter,
            IOptionsMonitor<PrinterOptions> printerOpts,
            ILogger<WebApplication> logger,
            CancellationToken ct) =>
        {
            var sw            = Stopwatch.StartNew();
            var correlationId = request.CorrelationId ?? Guid.NewGuid();
            var opts          = printerOpts.CurrentValue;
            var format        = opts.Format;

            logger.LogInformation(
                "Print request — CorrelationId={CorrelationId} Template={Template} Printer={Printer}",
                correlationId, request.TemplateName, request.PrinterName ?? opts.DefaultPrinterName ?? "(default)");

            // Step 1: Render via cloud
            CloudRenderResponse cloudResult;
            try
            {
                cloudResult = await cloud.RenderAsync(request, format, correlationId, ct);
            }
            catch (DocumentGeneratorException ex)
            {
                logger.LogError(ex,
                    "[{ErrorCode}] Cloud render failed — CorrelationId={CorrelationId}",
                    ex.ToString(), correlationId);
                return Results.Ok(PrintResponse.Fail(correlationId, ex.Message, sw.Elapsed, ex.ToString()));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Cloud render failed — CorrelationId={CorrelationId}", correlationId);
                return Results.Ok(PrintResponse.Fail(correlationId, $"Cloud render error: {ex.Message}", sw.Elapsed));
            }

            if (!cloudResult.Success || cloudResult.DocumentBase64 is null)
                return Results.Ok(PrintResponse.Fail(correlationId, cloudResult.Error ?? "Cloud render failed.", sw.Elapsed));

            // Step 2: Decode bytes
            byte[] docBytes;
            try
            {
                docBytes = Convert.FromBase64String(cloudResult.DocumentBase64);
            }
            catch (FormatException ex)
            {
                var decodeEx = PrintException.DecodeFailed(ex);
                logger.LogError(ex,
                    "[{ErrorCode}] Base64 decode failed — CorrelationId={CorrelationId}",
                    decodeEx.ToString(), correlationId);
                return Results.Ok(PrintResponse.Fail(correlationId, decodeEx.Message, sw.Elapsed, decodeEx.ToString()));
            }

            // Step 3: Send to local printer
            var printerName = !string.IsNullOrWhiteSpace(request.PrinterName)
                ? request.PrinterName
                : opts.DefaultPrinterName;

            var jobName = request.Variables.TryGetValue("firstName", out var fn) &&
                          request.Variables.TryGetValue("lastName",  out var ln)
                ? $"Badge – {fn} {ln}"
                : $"Badge – {request.TemplateName}";

            var printResult = await printerAdapter.PrintAsync(
                docBytes,
                cloudResult.MimeType ?? "application/pdf",
                printerName,
                jobName,
                ct);

            if (!printResult.Success)
            {
                logger.LogWarning(
                    "[{ErrorCode}] Print failed (document still returned) — CorrelationId={CorrelationId} Error={Error}",
                    printResult.ErrorCode ?? "DG5003", correlationId, printResult.Error);

                // Return the document even if printing failed so the iPad still has it
                return Results.Ok(new PrintResponse
                {
                    CorrelationId  = correlationId,
                    Success        = false,
                    DocumentBase64 = cloudResult.DocumentBase64,
                    MimeType       = cloudResult.MimeType,
                    Printed        = false,
                    PrinterUsed    = printResult.PrinterUsed,
                    Error          = $"Print spooler error: {printResult.Error}",
                    ErrorCode      = printResult.ErrorCode ?? "DG5003",
                    ElapsedTime    = sw.Elapsed,
                    CompletedAt    = DateTimeOffset.UtcNow
                });
            }

            logger.LogInformation(
                "Print complete — CorrelationId={CorrelationId} Printer={Printer} Elapsed={Elapsed}ms",
                correlationId, printResult.PrinterUsed, sw.Elapsed.TotalMilliseconds);

            return Results.Ok(PrintResponse.PrintOk(
                correlationId,
                cloudResult.DocumentBase64,
                cloudResult.MimeType ?? "application/pdf",
                printResult.PrinterUsed ?? "unknown",
                sw.Elapsed));
        })
        .WithName("Print")
        .WithSummary("Renders a badge via the cloud API, prints it locally, and returns Base64 bytes.");
    }
}

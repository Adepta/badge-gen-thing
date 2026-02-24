using System.Diagnostics;
using DocumentGenerator.Core.Interfaces;
using DocumentGenerator.Core.Models;
using Microsoft.Extensions.Logging;

namespace DocumentGenerator.Pdf;

/// <summary>
/// Orchestrates template rendering → PDF conversion.
/// This is the single entry point for all callers.
/// </summary>
public sealed class DocumentPipeline : IDocumentPipeline
{
    private readonly ITemplateEngine _templateEngine;
    private readonly IDocumentRenderer _renderer;
    private readonly ILogger<DocumentPipeline> _logger;

    /// <summary>
    /// Initialises the pipeline with its required dependencies.
    /// </summary>
    /// <param name="templateEngine">Engine used to render Handlebars templates to HTML.</param>
    /// <param name="renderer">Renderer used to convert HTML to PDF bytes.</param>
    /// <param name="logger">Logger for pipeline lifecycle events.</param>
    public DocumentPipeline(
        ITemplateEngine templateEngine,
        IDocumentRenderer renderer,
        ILogger<DocumentPipeline> logger)
    {
        _templateEngine = templateEngine;
        _renderer       = renderer;
        _logger         = logger;
    }

    /// <summary>
    /// Executes the full render pipeline: template → HTML → PDF.
    /// </summary>
    /// <param name="request">The render job to execute.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A <see cref="RenderResult"/> containing the PDF bytes and elapsed time.</returns>
    public async Task<RenderResult> ExecuteAsync(RenderRequest request, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        _logger.LogInformation(
            "Starting render job {JobId} — documentType: {DocumentType}",
            request.JobId, request.Template.DocumentType);

        try
        {
            // Step 1: Resolve Handlebars template → HTML
            var html = await _templateEngine.RenderAsync(request.Template, cancellationToken);

            // Step 2: Render HTML → PDF bytes
            var pdfBytes = await _renderer.RenderPdfAsync(html, request.Template.Pdf, cancellationToken);

            sw.Stop();

            _logger.LogInformation(
                "Render job {JobId} completed in {Elapsed}ms — {Bytes:N0} bytes",
                request.JobId, sw.ElapsedMilliseconds, pdfBytes.Length);

            return RenderResult.Success(request.JobId, pdfBytes, sw.Elapsed, request.Template.DocumentType);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Render job {JobId} failed after {Elapsed}ms", request.JobId, sw.ElapsedMilliseconds);
            throw;
        }
    }
}

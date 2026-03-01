using System.Diagnostics;
using DocumentGenerator.Core.Errors;
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
    private readonly ITemplateContentResolver _contentResolver;
    private readonly IDocumentRenderer _renderer;
    private readonly ILogger<DocumentPipeline> _logger;

    /// <summary>
    /// Initialises the pipeline with its required dependencies.
    /// </summary>
    /// <param name="templateEngine">Engine used to render Handlebars templates to HTML.</param>
    /// <param name="contentResolver">Resolver that loads HTML/CSS from disk when paths are set.</param>
    /// <param name="renderer">Renderer used to convert HTML to PDF bytes.</param>
    /// <param name="logger">Logger for pipeline lifecycle events.</param>
    public DocumentPipeline(
        ITemplateEngine templateEngine,
        ITemplateContentResolver contentResolver,
        IDocumentRenderer renderer,
        ILogger<DocumentPipeline> logger)
    {
        _templateEngine  = templateEngine;
        _contentResolver = contentResolver;
        _renderer        = renderer;
        _logger          = logger;
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
        var docType = request.Template.DocumentType;

        // Emit an OTel span for the entire pipeline execution.
        using var activity = DocumentGeneratorTelemetry.Source.StartActivity("render.pipeline");
        activity?.SetTag("document_type", docType);
        activity?.SetTag("job_id",        request.JobId.ToString());

        // Push JobId + DocumentType into the logging scope so all downstream
        // log calls (renderer, browser pool) carry these properties automatically.
        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["JobId"]        = request.JobId,
            ["DocumentType"] = docType
        });

        _logger.LogInformation(
            "Pipeline start — JobId: {JobId}, DocumentType: {DocumentType}",
            request.JobId, docType);

        try
        {
            // Step 1: Load HtmlPath / CssPath from disk into inline strings.
            // basePath is empty because TemplateLocator always stores absolute paths.
            // This is a no-op when Html is already populated inline (e.g. in unit tests).
            var resolved = await _contentResolver.ResolveAsync(
                request.Template, basePath: string.Empty, cancellationToken);

            // Step 2: Resolve Handlebars template → HTML
            var html = await _templateEngine.RenderAsync(resolved, cancellationToken);

            _logger.LogDebug(
                "Template rendered to HTML — JobId: {JobId}, HtmlLength: {HtmlLength}",
                request.JobId, html.Length);

            // Step 3: Render HTML → PDF bytes
            var pdfBytes = await _renderer.RenderPdfAsync(html, request.Template.Pdf, cancellationToken);

            if (pdfBytes.Length == 0)
                throw RenderException.EmptyOutput(request.JobId);

            sw.Stop();
            var elapsedMs = sw.Elapsed.TotalMilliseconds;

            // Record metrics — success path.
            var tags = new TagList
            {
                { "document_type", docType },
                { "success",       "true"  }
            };
            DocumentGeneratorTelemetry.RenderDuration.Record(elapsedMs, tags);
            DocumentGeneratorTelemetry.RenderCount.Add(1, tags);

            activity?.SetTag("success", true);

            _logger.LogInformation(
                "Pipeline complete — JobId: {JobId}, DocumentType: {DocumentType}, " +
                "Bytes: {Bytes}, ElapsedMs: {ElapsedMs}",
                request.JobId, docType, pdfBytes.Length, sw.ElapsedMilliseconds);

            return RenderResult.Success(request.JobId, pdfBytes, sw.Elapsed, docType);
        }
        catch (DocumentGeneratorException)
        {
            // Already a typed domain exception — log and re-throw without wrapping.
            sw.Stop();
            var elapsedMs = sw.Elapsed.TotalMilliseconds;
            var tags = new TagList { { "document_type", docType }, { "success", "false" } };
            DocumentGeneratorTelemetry.RenderDuration.Record(elapsedMs, tags);
            DocumentGeneratorTelemetry.RenderCount.Add(1, tags);
            activity?.SetStatus(ActivityStatusCode.Error);
            _logger.LogError(
                "Pipeline failed — JobId: {JobId}, DocumentType: {DocumentType}, ElapsedMs: {ElapsedMs}",
                request.JobId, docType, sw.ElapsedMilliseconds);
            throw;
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            activity?.SetStatus(ActivityStatusCode.Error, "cancelled");
            _logger.LogWarning(
                "Pipeline cancelled — JobId: {JobId}, ElapsedMs: {ElapsedMs}",
                request.JobId, sw.ElapsedMilliseconds);
            throw;
        }
        catch (Exception ex)
        {
            sw.Stop();
            var elapsedMs = sw.Elapsed.TotalMilliseconds;
            var tags = new TagList { { "document_type", docType }, { "success", "false" } };
            DocumentGeneratorTelemetry.RenderDuration.Record(elapsedMs, tags);
            DocumentGeneratorTelemetry.RenderCount.Add(1, tags);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _logger.LogError(ex,
                "Pipeline failed — JobId: {JobId}, DocumentType: {DocumentType}, ElapsedMs: {ElapsedMs}",
                request.JobId, docType, sw.ElapsedMilliseconds);
            throw RenderException.PipelineFailed(request.JobId, ex);
        }
    }
}

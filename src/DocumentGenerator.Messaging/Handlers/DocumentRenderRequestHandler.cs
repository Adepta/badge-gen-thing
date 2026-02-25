using DocumentGenerator.Core.Interfaces;
using DocumentGenerator.Core.Models;
using DocumentGenerator.Messaging.Configuration;
using DocumentGenerator.Messaging.Messages;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Rebus.Bus;
using Rebus.Handlers;

namespace DocumentGenerator.Messaging.Handlers;

/// <summary>
/// Rebus message handler for <see cref="DocumentRenderRequest"/>.
///
/// Rebus dispatches one instance per message on the render.requests topic.
/// Concurrency is controlled by Rebus worker thread count, kept at or
/// below the Chromium pool MaxSize to avoid starvation.
///
/// On success  → replies <see cref="DocumentRenderResult"/> to the sender's return address.
///               When <see cref="DocumentRenderRequest.ReturnPdfInline"/> is <see langword="true"/>
///               (the default) the PDF is Base64-encoded in the reply message.
///               When <see langword="false"/>, the PDF is saved to
///               <see cref="KafkaOptions.PdfOutputPath"/> and the path is returned instead,
///               keeping the Kafka message small for devices that can reach shared storage.
/// On failure  → replies failure result (Rebus handles dead-lettering after MaxRetries).
/// </summary>
public sealed class DocumentRenderRequestHandler : IHandleMessages<DocumentRenderRequest>
{
    private readonly IDocumentPipeline _pipeline;
    private readonly IBus              _bus;
    private readonly IRenderMetrics    _metrics;
    private readonly KafkaOptions      _kafkaOptions;
    private readonly ILogger<DocumentRenderRequestHandler> _logger;

    public DocumentRenderRequestHandler(
        IDocumentPipeline pipeline, IBus bus,
        IRenderMetrics metrics,
        IOptions<KafkaOptions> kafkaOptions,
        ILogger<DocumentRenderRequestHandler> logger)
    {
        _pipeline     = pipeline;
        _bus          = bus;
        _metrics      = metrics;
        _kafkaOptions = kafkaOptions.Value;
        _logger       = logger;
    }

    public async Task Handle(DocumentRenderRequest message)
    {
        _logger.LogInformation(
            "Handling render request — CorrelationId: {CorrelationId}, DeviceId: {DeviceId}, DocumentType: {DocumentType}, ReturnPdfInline: {ReturnPdfInline}",
            message.CorrelationId, message.DeviceId, message.Template.DocumentType, message.ReturnPdfInline);

        var renderJob = new RenderRequest
        {
            JobId    = message.CorrelationId,
            Template = message.Template
        };

        DocumentRenderResult result;

        try
        {
            var renderResult = await _pipeline.ExecuteAsync(renderJob);

            string? pdfPath = null;

            if (!message.ReturnPdfInline)
            {
                pdfPath = await SavePdfAsync(
                    renderResult.PdfBytes,
                    message.Template.DocumentType,
                    message.CorrelationId);
            }

            result = DocumentRenderResult.Succeeded(
                message.CorrelationId, message.DeviceId, message.SessionId,
                message.Template.DocumentType,
                renderResult.PdfBytes, renderResult.ElapsedTime,
                returnInline: message.ReturnPdfInline,
                pdfPath: pdfPath);

            _metrics.RecordSuccess();

            _logger.LogInformation(
                "Render succeeded — CorrelationId: {CorrelationId}, {Bytes:N0} bytes in {Elapsed}ms{PathSuffix}",
                message.CorrelationId, renderResult.PdfBytes.Length,
                (int)renderResult.ElapsedTime.TotalMilliseconds,
                pdfPath is null ? string.Empty : $", saved to {pdfPath}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Render failed — CorrelationId: {CorrelationId}, DeviceId: {DeviceId}",
                message.CorrelationId, message.DeviceId);

            _metrics.RecordFailure();

            result = DocumentRenderResult.Failed(
                message.CorrelationId, message.DeviceId, message.SessionId,
                message.Template.DocumentType, ex.Message);
        }

        // Reply routes the result back to the sender's return address automatically
        await _bus.Reply(result);
    }

    /// <summary>
    /// Saves <paramref name="pdfBytes"/> to <see cref="KafkaOptions.PdfOutputPath"/> and
    /// returns the absolute path of the written file.
    /// </summary>
    private async Task<string> SavePdfAsync(byte[] pdfBytes, string documentType, Guid correlationId)
    {
        var outputDir = Path.GetFullPath(_kafkaOptions.PdfOutputPath);
        Directory.CreateDirectory(outputDir);

        var fileName   = $"{documentType}_{correlationId:N}.pdf";
        var outputPath = Path.Combine(outputDir, fileName);

        await File.WriteAllBytesAsync(outputPath, pdfBytes);

        _logger.LogDebug("PDF saved to {Path}", outputPath);

        return outputPath;
    }
}

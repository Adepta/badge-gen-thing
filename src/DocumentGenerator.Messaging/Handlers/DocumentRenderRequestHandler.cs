using DocumentGenerator.Core.Errors;
using DocumentGenerator.Core.Interfaces;
using DocumentGenerator.Core.Models;
using DocumentGenerator.Messaging.Configuration;
using DocumentGenerator.Messaging.Messages;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Rebus.Bus;
using Rebus.Handlers;
using Rebus.Messages;
using Rebus.Pipeline;

namespace DocumentGenerator.Messaging.Handlers;

/// <summary>
/// Rebus message handler for <see cref="DocumentRenderRequest"/>.
///
/// Rebus dispatches one instance per message on the render.requests topic.
/// Concurrency is controlled by Rebus worker thread count, kept at or
/// below the Chromium pool MaxSize to avoid starvation.
///
/// Error handling strategy:
/// <list type="bullet">
///   <item>
///     <term>Transient failures (<see cref="BrowserPoolException"/>)</term>
///     <description>
///       Re-thrown so Rebus retries up to <see cref="KafkaOptions.MaxRetries"/> times
///       with exponential backoff. After all retries are exhausted Rebus dead-letters
///       the message to <see cref="KafkaOptions.DeadLetterTopic"/>.
///     </description>
///   </item>
///   <item>
///     <term>Domain failures (other <see cref="DocumentGeneratorException"/>)</term>
///     <description>
///       Replied as a failure <see cref="DocumentRenderResult"/> — retrying would not help.
///     </description>
///   </item>
///   <item>
///     <term>Unexpected exceptions</term>
///     <description>
///       Replied as a generic DG9001 failure. The exception is logged.
///     </description>
///   </item>
/// </list>
/// </summary>
public sealed class DocumentRenderRequestHandler : IHandleMessages<DocumentRenderRequest>
{
    private readonly IDocumentPipeline _pipeline;
    private readonly IBus              _bus;
    private readonly IRenderMetrics    _metrics;
    private readonly KafkaOptions      _kafkaOptions;
    private readonly ILogger<DocumentRenderRequestHandler> _logger;

    /// <summary>Initialises the handler with its required dependencies.</summary>
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

    /// <summary>
    /// Executes the render pipeline for the incoming <paramref name="message"/>,
    /// then replies with a <see cref="DocumentRenderResult"/> indicating success or failure.
    /// </summary>
    public async Task Handle(DocumentRenderRequest message)
    {
        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = message.CorrelationId,
            ["DeviceId"]      = message.DeviceId,
            ["SessionId"]     = message.SessionId ?? string.Empty,
            ["DocumentType"]  = message.Template.DocumentType
        });

        _logger.LogInformation(
            "Handling render request — CorrelationId: {CorrelationId}, DeviceId: {DeviceId}, " +
            "DocumentType: {DocumentType}, ReturnPdfInline: {ReturnPdfInline}",
            message.CorrelationId, message.DeviceId,
            message.Template.DocumentType, message.ReturnPdfInline);

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
                "Render succeeded — CorrelationId: {CorrelationId}, Bytes: {Bytes}, " +
                "ElapsedMs: {ElapsedMs}, PdfPath: {PdfPath}",
                message.CorrelationId, renderResult.PdfBytes.Length,
                (int)renderResult.ElapsedTime.TotalMilliseconds,
                pdfPath ?? "(inline)");
        }
        catch (BrowserPoolException ex)
        {
            // Transient — re-throw so Rebus can retry with backoff and eventually dead-letter.
            _logger.LogWarning(ex,
                "[{ErrorCode}] Transient browser pool failure — CorrelationId: {CorrelationId}. " +
                "Rebus will retry (max {MaxRetries}).",
                ex.Code, message.CorrelationId, _kafkaOptions.MaxRetries);

            _metrics.RecordFailure();
            throw; // Let Rebus handle retry + dead-lettering
        }
        catch (DocumentGeneratorException ex)
        {
            // Non-transient domain exception — reply with failure, do not retry.
            _logger.LogError(ex,
                "[{ErrorCode}] Render failed — CorrelationId: {CorrelationId}, DeviceId: {DeviceId}, " +
                "DocumentType: {DocumentType}",
                ex.Code, message.CorrelationId, message.DeviceId, message.Template.DocumentType);

            _metrics.RecordFailure();

            result = DocumentRenderResult.Failed(
                message.CorrelationId, message.DeviceId, message.SessionId,
                message.Template.DocumentType, ex.Message, ex.Code.ToString());
        }
        catch (Exception ex)
        {
            // Unexpected exception — reply with failure, do not retry.
            _logger.LogError(ex,
                "[DG9001] Unexpected render failure — CorrelationId: {CorrelationId}, DeviceId: {DeviceId}, " +
                "DocumentType: {DocumentType}",
                message.CorrelationId, message.DeviceId, message.Template.DocumentType);

            _metrics.RecordFailure();

            result = DocumentRenderResult.Failed(
                message.CorrelationId, message.DeviceId, message.SessionId,
                message.Template.DocumentType, ex.Message, "DG9001");
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

using DocumentGenerator.Core.Interfaces;
using DocumentGenerator.Core.Models;
using DocumentGenerator.Messaging.Messages;
using Microsoft.Extensions.Logging;
using Rebus.Bus;
using Rebus.Handlers;

namespace DocumentGenerator.Messaging.Handlers;

/// <summary>
/// Rebus message handler for <see cref="DocumentRenderRequest"/>.
///
/// Rebus dispatches one instance per message on the <c>render.requests</c> topic.
/// Concurrency is controlled by Rebus worker thread count, which is kept at or
/// below the Chromium pool MaxSize to avoid starvation.
///
/// On success  → replies <see cref="DocumentRenderResult"/> to the sender's return address
/// On failure  → replies failure result (Rebus handles dead-lettering after MaxRetries)
/// </summary>
public sealed class DocumentRenderRequestHandler : IHandleMessages<DocumentRenderRequest>
{
    private readonly IDocumentPipeline _pipeline;
    private readonly IBus              _bus;
    private readonly IRenderMetrics    _metrics;
    private readonly ILogger<DocumentRenderRequestHandler> _logger;

    /// <summary>
    /// Initialises the handler with its required dependencies.
    /// </summary>
    /// <param name="pipeline">The document rendering pipeline.</param>
    /// <param name="bus">The Rebus bus used to reply to the sender.</param>
    /// <param name="metrics">Render outcome metrics recorder.</param>
    /// <param name="logger">Logger for this handler.</param>
    public DocumentRenderRequestHandler(
        IDocumentPipeline pipeline,
        IBus              bus,
        IRenderMetrics    metrics,
        ILogger<DocumentRenderRequestHandler> logger)
    {
        _pipeline = pipeline;
        _bus      = bus;
        _metrics  = metrics;
        _logger   = logger;
    }

    /// <summary>
    /// Handles a <see cref="DocumentRenderRequest"/> by executing the render pipeline
    /// and replying with a <see cref="DocumentRenderResult"/> (success or failure).
    /// </summary>
    /// <param name="message">The incoming render request.</param>
    public async Task Handle(DocumentRenderRequest message)
    {
        _logger.LogInformation(
            "Handling render request — CorrelationId: {CorrelationId}, DeviceId: {DeviceId}, DocumentType: {DocumentType}",
            message.CorrelationId, message.DeviceId, message.Template.DocumentType);

        var renderJob = new RenderRequest
        {
            JobId    = message.CorrelationId,
            Template = message.Template
        };

        DocumentRenderResult result;

        try
        {
            var renderResult = await _pipeline.ExecuteAsync(renderJob);

            result = DocumentRenderResult.Succeeded(
                message.CorrelationId,
                message.DeviceId,
                message.SessionId,
                message.Template.DocumentType,
                renderResult.PdfBytes,
                renderResult.ElapsedTime);

            _metrics.RecordSuccess();

            _logger.LogInformation(
                "Render succeeded — CorrelationId: {CorrelationId}, {Bytes:N0} bytes in {Elapsed}ms",
                message.CorrelationId,
                renderResult.PdfBytes.Length,
                (int)renderResult.ElapsedTime.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            // Let Rebus handle retries — it will rethrow after MaxDeliveryAttempts
            // and route to the error queue automatically.
            _logger.LogError(ex,
                "Render failed — CorrelationId: {CorrelationId}, DeviceId: {DeviceId}",
                message.CorrelationId, message.DeviceId);

            _metrics.RecordFailure();

            result = DocumentRenderResult.Failed(
                message.CorrelationId,
                message.DeviceId,
                message.SessionId,
                message.Template.DocumentType,
                ex.Message);
        }

        // Reply routes the result back to the sender's return address automatically
        await _bus.Reply(result);
    }
}

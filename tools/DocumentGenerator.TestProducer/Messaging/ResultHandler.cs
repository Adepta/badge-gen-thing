using DocumentGenerator.Messaging.Messages;
using Microsoft.Extensions.Logging;
using Rebus.Handlers;

namespace DocumentGenerator.TestProducer.Messaging;

/// <summary>
/// Rebus message handler that receives <see cref="DocumentRenderResult"/> replies
/// from the render service and resolves the matching <see cref="ResultStore"/> awaiter.
/// </summary>
public sealed class ResultHandler(ResultStore store, ILogger<ResultHandler> logger)
    : IHandleMessages<DocumentRenderResult>
{
    public Task Handle(DocumentRenderResult message)
    {
        logger.LogDebug(
            "Render result received — CorrelationId: {CorrelationId}, Success: {Success}, " +
            "DocumentType: {DocumentType}, ElapsedMs: {ElapsedMs}",
            message.CorrelationId,
            message.Success,
            message.DocumentType,
            (int)message.ElapsedTime.TotalMilliseconds);

        store.Complete(message);
        return Task.CompletedTask;
    }
}

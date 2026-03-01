using DocumentGenerator.Messaging.Messages;
using Rebus.Handlers;

namespace DocumentGenerator.Api.Messaging;

/// <summary>
/// Rebus message handler that receives <see cref="DocumentRenderResult"/> messages from
/// the <c>render.results</c> Kafka topic and resolves the matching in-process awaiter
/// held by <see cref="PendingRenderStore"/>.
///
/// <para>
/// Because each API instance subscribes with a unique consumer group, every result message
/// is delivered to every instance. This handler resolves the awaiter if the correlation ID
/// is locally registered, and silently discards the message otherwise.
/// </para>
/// </summary>
public sealed class DocumentRenderResultHandler(
    PendingRenderStore store,
    ILogger<DocumentRenderResultHandler> logger)
    : IHandleMessages<DocumentRenderResult>
{
    /// <summary>
    /// Handles an inbound <see cref="DocumentRenderResult"/> by resolving the matching
    /// <see cref="PendingRenderStore"/> awaiter, if one exists in this process.
    /// </summary>
    /// <param name="message">The result message received from Kafka.</param>
    public Task Handle(DocumentRenderResult message)
    {
        var resolved = store.TryComplete(message);

        if (resolved)
        {
            logger.LogInformation(
                "Render result resolved — CorrelationId={CorrelationId} Success={Success} " +
                "DocumentType={DocumentType} ElapsedMs={ElapsedMs}",
                message.CorrelationId,
                message.Success,
                message.DocumentType,
                (int)message.ElapsedTime.TotalMilliseconds);
        }
        else
        {
            logger.LogDebug(
                "Render result discarded (not owned by this instance) — CorrelationId={CorrelationId}",
                message.CorrelationId);
        }

        return Task.CompletedTask;
    }
}

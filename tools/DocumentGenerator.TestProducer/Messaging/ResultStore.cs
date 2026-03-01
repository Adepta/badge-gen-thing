using System.Collections.Concurrent;
using DocumentGenerator.Messaging.Messages;

namespace DocumentGenerator.TestProducer.Messaging;

/// <summary>
/// Thread-safe in-memory registry that maps a render job's <see cref="Guid"/> correlation ID
/// to its awaiting <see cref="TaskCompletionSource{T}"/>.
///
/// The worker registers a TCS before publishing the Kafka request; the
/// <see cref="ResultHandler"/> resolves it when the reply arrives.
/// </summary>
public sealed class ResultStore
{
    private readonly ConcurrentDictionary<Guid, TaskCompletionSource<DocumentRenderResult>> _pending = new();

    /// <summary>
    /// Registers a pending result awaiter for the given correlation ID.
    /// </summary>
    public void Register(Guid correlationId, TaskCompletionSource<DocumentRenderResult> tcs) =>
        _pending[correlationId] = tcs;

    /// <summary>
    /// Resolves the awaiter for the correlation ID carried by <paramref name="result"/>.
    /// No-ops silently if the ID is unknown (e.g. a duplicate delivery).
    /// </summary>
    public void Complete(DocumentRenderResult result)
    {
        if (_pending.TryRemove(result.CorrelationId, out var tcs))
            tcs.TrySetResult(result);
    }

    /// <summary>
    /// Cancels all pending awaiters — called on graceful shutdown so that
    /// in-flight jobs do not hang indefinitely.
    /// </summary>
    public void CancelAll()
    {
        foreach (var (_, tcs) in _pending)
            tcs.TrySetCanceled();

        _pending.Clear();
    }

    /// <summary>Number of jobs currently awaiting a result.</summary>
    public int PendingCount => _pending.Count;
}

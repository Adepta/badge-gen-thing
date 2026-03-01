using System.Collections.Concurrent;
using DocumentGenerator.Messaging.Messages;

namespace DocumentGenerator.Api.Messaging;

/// <summary>
/// Thread-safe in-process store that maps a <see cref="DocumentRenderRequest.CorrelationId"/>
/// to the <see cref="TaskCompletionSource{T}"/> awaiting its result.
///
/// <para>
/// Lifetime: Singleton — one store per API process. Each API instance gets a unique
/// Rebus consumer group so <em>every</em> <see cref="DocumentRenderResult"/> message on
/// <c>render.results</c> is delivered to every instance. The store resolves only the ones
/// whose correlation IDs are locally registered and silently discards the rest.
/// </para>
/// </summary>
public sealed class PendingRenderStore
{
    private readonly ConcurrentDictionary<Guid, TaskCompletionSource<DocumentRenderResult>> _pending = new();

    /// <summary>Number of render requests currently awaiting a result.</summary>
    public int PendingCount => _pending.Count;

    /// <summary>
    /// Registers a new awaiter for the given <paramref name="correlationId"/> and returns a
    /// <see cref="Task{T}"/> that completes when the matching result arrives from Kafka.
    /// </summary>
    /// <param name="correlationId">Unique ID assigned to the outbound render request.</param>
    /// <param name="cancellationToken">
    /// Token used to time out or cancel the wait (e.g. the 25-second API timeout or client disconnect).
    /// When fired the task transitions to <see cref="TaskStatus.Canceled"/> and the awaiter is removed.
    /// </param>
    /// <returns>
    /// A <see cref="Task{T}"/> that completes when <see cref="TryComplete"/> is called with the
    /// matching correlation ID, or is cancelled when <paramref name="cancellationToken"/> fires.
    /// </returns>
    public Task<DocumentRenderResult> RegisterAsync(Guid correlationId, CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<DocumentRenderResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        _pending[correlationId] = tcs;

        cancellationToken.Register(() =>
        {
            if (_pending.TryRemove(correlationId, out var removed))
                removed.TrySetCanceled(cancellationToken);
        });

        return tcs.Task;
    }

    /// <summary>
    /// Resolves the awaiter for <paramref name="result"/>'s correlation ID if one is registered
    /// in this process. Silently no-ops when the correlation ID is unknown (i.e. the result was
    /// published in response to a request from a different API instance).
    /// </summary>
    /// <param name="result">The completed render result received from Kafka.</param>
    /// <returns>
    /// <see langword="true"/> if a matching awaiter was found and resolved;
    /// <see langword="false"/> if the correlation ID is unknown to this instance.
    /// </returns>
    public bool TryComplete(DocumentRenderResult result)
    {
        if (!_pending.TryRemove(result.CorrelationId, out var tcs))
            return false;

        tcs.TrySetResult(result);
        return true;
    }

    /// <summary>
    /// Cancels all pending awaiters. Called during graceful shutdown to unblock in-flight
    /// HTTP requests rather than leaving them to time out individually.
    /// </summary>
    public void CancelAll()
    {
        foreach (var (id, tcs) in _pending)
        {
            if (_pending.TryRemove(id, out _))
                tcs.TrySetCanceled();
        }
    }
}

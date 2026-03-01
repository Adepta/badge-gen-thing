namespace DocumentGenerator.Api.Messaging;

/// <summary>
/// Hosted service that cancels all pending render awaiters in <see cref="PendingRenderStore"/>
/// during graceful shutdown, unblocking any in-flight HTTP requests immediately rather than
/// leaving them to time out individually.
/// </summary>
public sealed class PendingRenderShutdownService(
    PendingRenderStore store,
    ILogger<PendingRenderShutdownService> logger)
    : IHostedService
{
    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Cancelling {Count} pending render awaiters on shutdown.", store.PendingCount);

        store.CancelAll();
        return Task.CompletedTask;
    }
}

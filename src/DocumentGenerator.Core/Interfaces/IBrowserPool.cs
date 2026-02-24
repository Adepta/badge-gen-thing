namespace DocumentGenerator.Core.Interfaces;

/// <summary>
/// Manages a pool of reusable Chromium browser instances.
/// Callers lease a browser, use it, then return it — similar to a
/// database connection pool.
/// </summary>
/// <typeparam name="TBrowser">
/// The concrete browser type (e.g. PuppeteerSharp.IBrowser).
/// Keeping it generic prevents Core from depending on PuppeteerSharp.
/// </typeparam>
public interface IBrowserPool<TBrowser> : IAsyncDisposable
{
    /// <summary>
    /// Leases a browser from the pool. If all instances are busy and the
    /// pool has not reached MaxSize, a new instance is launched.
    /// Otherwise the caller waits until one is returned or the timeout elapses.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the wait.</param>
    /// <returns>
    /// An <see cref="IBrowserLease{TBrowser}"/> that must be disposed after use
    /// to return the browser to the pool.
    /// </returns>
    /// <exception cref="TimeoutException">Thrown when no browser becomes available within the configured acquire timeout.</exception>
    /// <exception cref="ObjectDisposedException">Thrown if the pool has already been disposed.</exception>
    Task<IBrowserLease<TBrowser>> AcquireAsync(CancellationToken cancellationToken = default);

    /// <summary>Current number of browser instances in the pool (idle + busy).</summary>
    int PoolSize { get; }

    /// <summary>Number of browser instances currently executing a render.</summary>
    int ActiveCount { get; }
}

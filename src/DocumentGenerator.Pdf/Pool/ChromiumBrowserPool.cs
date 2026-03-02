using System.Collections.Concurrent;
using System.Diagnostics;
using DocumentGenerator.Core.Configuration;
using DocumentGenerator.Core.Errors;
using DocumentGenerator.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PuppeteerSharp;

namespace DocumentGenerator.Pdf.Pool;

/// <summary>
/// A thread-safe pool of PuppeteerSharp Chromium browser instances.
///
/// Strategy:
/// - A <see cref="SemaphoreSlim"/> caps concurrent leases at <see cref="BrowserPoolOptions.MaxSize"/>.
/// - An idle queue (<see cref="ConcurrentQueue{T}"/>) hands out warm browsers instantly.
/// - When the idle queue is empty but the semaphore permits, a new browser is launched.
/// - Each instance tracks render count; when <see cref="BrowserPoolOptions.MaxRendersPerInstance"/>
///   is reached the browser is recycled rather than returned to the idle queue.
/// - A background reaper task closes browsers that have been idle longer than
///   <see cref="BrowserPoolOptions.IdleTimeout"/> (while keeping at least MinSize warm).
/// </summary>
public sealed class ChromiumBrowserPool : IBrowserPool<IBrowser>
{
    private readonly BrowserPoolOptions _options;
    private readonly ILogger<ChromiumBrowserPool> _logger;
    private readonly SemaphoreSlim _semaphore;
    private readonly ConcurrentQueue<PooledBrowser> _idle = new();
    private readonly ConcurrentDictionary<IBrowser, PooledBrowser> _all = new();
    private readonly CancellationTokenSource _reaperCts = new();
    private readonly Task _reaperTask;
    private int _activeCount;
    private bool _disposed;

    /// <summary>Total number of browser instances currently tracked by the pool (idle + active).</summary>
    public int PoolSize  => _all.Count;

    /// <summary>Number of browser instances currently leased out to callers.</summary>
    public int ActiveCount => _activeCount;

    /// <summary>
    /// Initialises the pool, starts the idle reaper background task, and logs configuration.
    /// </summary>
    /// <param name="options">Pool configuration (min/max size, idle timeout, recycle limit).</param>
    /// <param name="logger">Logger for pool lifecycle events.</param>
    public ChromiumBrowserPool(IOptions<BrowserPoolOptions> options, ILogger<ChromiumBrowserPool> logger)
    {
        _options = options.Value;
        _logger  = logger;
        _semaphore = new SemaphoreSlim(_options.MaxSize, _options.MaxSize);

        _logger.LogInformation(
            "Chromium pool initialised — min: {Min}, max: {Max}, maxRenders: {MaxRenders}, idleTimeout: {IdleTimeout}",
            _options.MinSize, _options.MaxSize, _options.MaxRendersPerInstance, _options.IdleTimeout);

        _reaperTask = RunReaperAsync(_reaperCts.Token);
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Acquires a browser lease from the pool, waiting up to <see cref="BrowserPoolOptions.AcquireTimeout"/>
    /// for an available slot. Throws <see cref="TimeoutException"/> if no browser becomes available in time.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the wait.</param>
    /// <returns>A lease that must be disposed to return the browser to the pool.</returns>
    /// <exception cref="BrowserPoolException">
    /// Thrown with <see cref="ErrorCode.BrowserPoolDisposed"/> if the pool is disposed,
    /// or <see cref="ErrorCode.BrowserPoolTimeout"/> if the acquire timeout elapses.
    /// </exception>
    public async Task<IBrowserLease<IBrowser>> AcquireAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw BrowserPoolException.Disposed();

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_options.AcquireTimeout);

        try
        {
            await _semaphore.WaitAsync(timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw BrowserPoolException.AcquireTimeout(_options.AcquireTimeout, PoolSize, ActiveCount);
        }
        catch (ObjectDisposedException)
        {
            // Pool was disposed while we were waiting on the semaphore.
            throw BrowserPoolException.Disposed();
        }

        // Check disposed again — DisposeAsync may have run between WaitAsync returning and here.
        if (_disposed)
        {
            _semaphore.Release();
            throw BrowserPoolException.Disposed();
        }

        IBrowser browser;
        try
        {
            browser = await GetOrCreateBrowserAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            _semaphore.Release();
            throw;
        }

        Interlocked.Increment(ref _activeCount);
        _logger.LogDebug("Browser leased — pool: {PoolSize}, active: {Active}", PoolSize, ActiveCount);
        return new BrowserLease(browser, this);
    }

    // -------------------------------------------------------------------------
    // Internal — called by BrowserLease
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns <paramref name="browser"/> to the idle queue (or recycles it if its render
    /// count has reached <see cref="BrowserPoolOptions.MaxRendersPerInstance"/>).
    /// Called by <see cref="BrowserLease"/> on disposal when not invalidated.
    /// </summary>
    /// <param name="browser">The browser instance being returned.</param>
    internal async Task ReturnAsync(IBrowser browser)
    {
        if (!_all.TryGetValue(browser, out var pooled))
        {
            // Unknown browser — just close it
            await SafeCloseAsync(browser).ConfigureAwait(false);
            _semaphore.Release();
            Interlocked.Decrement(ref _activeCount);
            return;
        }

        pooled.IncrementRenderCount();
        pooled.LastReturnedAt = DateTimeOffset.UtcNow;
        Interlocked.Decrement(ref _activeCount);

        bool recycle = _options.MaxRendersPerInstance > 0
                    && pooled.RenderCount >= _options.MaxRendersPerInstance;

        if (recycle)
        {
            _logger.LogInformation(
                "Recycling browser after {RenderCount} renders (limit: {Max})",
                pooled.RenderCount, _options.MaxRendersPerInstance);
            await DiscardInternalAsync(browser).ConfigureAwait(false);
        }
        else
        {
            _idle.Enqueue(pooled);
            _logger.LogDebug("Browser returned to pool — pool: {PoolSize}, active: {Active}", PoolSize, ActiveCount);
        }

        _semaphore.Release();
    }

    /// <summary>
    /// Closes and removes <paramref name="browser"/> from the pool without returning it to the idle queue.
    /// Called by <see cref="BrowserLease"/> on disposal when <see cref="BrowserLease.Invalidate"/> was called.
    /// </summary>
    /// <param name="browser">The invalidated browser instance to discard.</param>
    internal async Task DiscardAsync(IBrowser browser)
    {
        await DiscardInternalAsync(browser).ConfigureAwait(false);
        Interlocked.Decrement(ref _activeCount);
        _semaphore.Release();
        _logger.LogDebug("Invalidated browser discarded — pool: {PoolSize}, active: {Active}", PoolSize, ActiveCount);
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private async Task<IBrowser> GetOrCreateBrowserAsync(CancellationToken ct)
    {
        // Try idle queue first
        while (_idle.TryDequeue(out var pooled))
        {
            if (pooled.Browser.IsConnected)
            {
                return pooled.Browser;
            }

            // Disconnected — discard silently and try next
            _logger.LogWarning("Stale browser found in idle queue, discarding");
            await DiscardInternalAsync(pooled.Browser).ConfigureAwait(false);
        }

        // No idle browser — launch a new one
        return await LaunchBrowserAsync(ct).ConfigureAwait(false);
    }

    private async Task<IBrowser> LaunchBrowserAsync(CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        _logger.LogInformation("Launching new Chromium instance (pool will have {Count} total)", _all.Count + 1);

        IBrowser browser;
        try
        {
            browser = await Puppeteer.LaunchAsync(new LaunchOptions
            {
                Headless = true,
                Args =
                [
                    "--no-sandbox",
                    "--disable-setuid-sandbox",
                    "--disable-dev-shm-usage",   // prevents /dev/shm OOM in containers
                    "--disable-gpu",
                    "--no-first-run",
                    "--mute-audio"
                    // NOTE: --disable-background-networking and --disable-extensions are
                    // intentionally omitted — they block external resources such as
                    // Google Fonts, causing pages to render blank.
                ]
            }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Chromium failed to launch");
            throw BrowserPoolException.LaunchFailed(ex);
        }

        var pooled = new PooledBrowser(browser);
        _all[browser] = pooled;

        browser.Disconnected += (_, _) =>
        {
            _logger.LogWarning("Chromium instance disconnected unexpectedly");
            if (_all.TryRemove(browser, out _))
            {
                // Release the semaphore slot so the pool can accept new leases.
                // The browser was either idle or active; either way the slot is now free.
                // (When active, BrowserLease.Dispose will also call Release — but
                //  Disconnected fires before Dispose in the crash path, so this is fine.)
                try { _semaphore.Release(); } catch (ObjectDisposedException) { /* pool being torn down */ }
            }
        };

        _logger.LogInformation("Chromium launched in {Elapsed}ms", sw.ElapsedMilliseconds);
        return browser;
    }

    private async Task DiscardInternalAsync(IBrowser browser)
    {
        _all.TryRemove(browser, out _);
        await SafeCloseAsync(browser).ConfigureAwait(false);
    }

    private static async Task SafeCloseAsync(IBrowser browser)
    {
        try { await browser.CloseAsync().ConfigureAwait(false); }
        catch { /* best-effort */ }
    }

    // -------------------------------------------------------------------------
    // Idle reaper — trims browsers that exceed the idle timeout
    // -------------------------------------------------------------------------

    private async Task RunReaperAsync(CancellationToken ct)
    {
        if (_options.IdleTimeout is null) return;

        var interval = TimeSpan.FromSeconds(Math.Max(30, _options.IdleTimeout.Value.TotalSeconds / 2));

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval, ct).ConfigureAwait(false);
                await ReapIdleBrowsersAsync().ConfigureAwait(false);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in browser pool reaper");
            }
        }
    }

    private async Task ReapIdleBrowsersAsync()
    {
        if (_options.IdleTimeout is null) return;

        var cutoff = DateTimeOffset.UtcNow - _options.IdleTimeout.Value;
        var keepAlive = _options.MinSize;
        var reaped = 0;

        // Snapshot idle items — we can't iterate the queue non-destructively,
        // so we drain and re-enqueue items we want to keep.
        var snapshot = new List<PooledBrowser>();
        while (_idle.TryDequeue(out var item)) snapshot.Add(item);

        // Newest-first so we keep the most recently used browsers warm
        snapshot.Sort(static (a, b) => b.LastReturnedAt.CompareTo(a.LastReturnedAt));

        for (int i = 0; i < snapshot.Count; i++)
        {
            var pooled = snapshot[i];
            bool isOld = pooled.LastReturnedAt < cutoff;
            bool canReap = _all.Count - reaped > keepAlive;

            if (isOld && canReap)
            {
                await DiscardInternalAsync(pooled.Browser).ConfigureAwait(false);
                reaped++;
            }
            else
            {
                _idle.Enqueue(pooled);
            }
        }

        if (reaped > 0)
            _logger.LogInformation("Reaped {Count} idle Chromium instances (pool size: {PoolSize})", reaped, PoolSize);
    }

    // -------------------------------------------------------------------------
    // Disposal
    // -------------------------------------------------------------------------

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        _logger.LogInformation("Shutting down Chromium pool — closing {Count} instances", _all.Count);

        await _reaperCts.CancelAsync().ConfigureAwait(false);
        try { await _reaperTask.ConfigureAwait(false); } catch { /* ignore */ }

        foreach (var (browser, _) in _all)
        {
            await SafeCloseAsync(browser).ConfigureAwait(false);
        }

        _all.Clear();
        _semaphore.Dispose();
        _reaperCts.Dispose();
    }
}

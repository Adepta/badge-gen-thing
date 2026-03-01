using DocumentGenerator.Core.Interfaces;
using PuppeteerSharp;

namespace DocumentGenerator.Pdf.Pool;

/// <summary>
/// A leased PuppeteerSharp browser instance. Disposing returns it to the pool
/// unless <see cref="Invalidate"/> has been called, in which case the browser
/// is closed and discarded.
/// </summary>
internal sealed class BrowserLease : IBrowserLease<IBrowser>
{
    private readonly ChromiumBrowserPool _pool;
    private bool _invalidated;
    private bool _disposed;

    /// <summary>The leased Puppeteer browser instance.</summary>
    public IBrowser Browser { get; }

    /// <summary>
    /// Initialises a new lease wrapping <paramref name="browser"/>.
    /// </summary>
    /// <param name="browser">The Chromium browser instance obtained from the pool.</param>
    /// <param name="pool">The pool that owns <paramref name="browser"/> and must be notified on disposal.</param>
    internal BrowserLease(IBrowser browser, ChromiumBrowserPool pool)
    {
        Browser = browser;
        _pool = pool;
    }

    /// <inheritdoc/>
    public void Invalidate() => _invalidated = true;

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        if (_invalidated)
        {
            await _pool.DiscardAsync(Browser);
        }
        else
        {
            await _pool.ReturnAsync(Browser);
        }
    }
}

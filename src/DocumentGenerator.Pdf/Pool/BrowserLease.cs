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

    public IBrowser Browser { get; }

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

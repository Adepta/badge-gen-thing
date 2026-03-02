using DocumentGenerator.Core.Interfaces;
using PuppeteerSharp;

namespace DocumentGenerator.Pdf.Pool;

/// <summary>
/// A leased PuppeteerSharp browser instance. Disposing returns it to the pool
/// unless <see cref="Invalidate"/> has been called, in which case the browser
/// is closed and discarded.
/// </summary>
internal sealed class BrowserLease(IBrowser browser, ChromiumBrowserPool pool) : IBrowserLease<IBrowser>
{
    private readonly ChromiumBrowserPool _pool = pool;
    private bool _invalidated;
    private bool _disposed;

    /// <summary>The leased Puppeteer browser instance.</summary>
    public IBrowser Browser { get; } = browser;

    /// <inheritdoc/>
    public void Invalidate() => _invalidated = true;

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        if (_invalidated)
        {
            await _pool.DiscardAsync(Browser).ConfigureAwait(false);
        }
        else
        {
            await _pool.ReturnAsync(Browser).ConfigureAwait(false);
        }
    }
}

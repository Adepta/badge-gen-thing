namespace DocumentGenerator.Core.Interfaces;

/// <summary>
/// Represents a leased Chromium browser instance from the pool.
/// Disposing the lease returns the browser to the pool rather than
/// closing it, unless the browser has crashed or been invalidated.
/// </summary>
/// <remarks>
/// TBrowser is kept generic so Core has zero dependency on PuppeteerSharp.
/// The Pdf project binds TBrowser to PuppeteerSharp.IBrowser.
/// </remarks>
public interface IBrowserLease<TBrowser> : IAsyncDisposable
{
    /// <summary>The underlying browser instance (e.g. PuppeteerSharp.IBrowser).</summary>
    TBrowser Browser { get; }

    /// <summary>
    /// Marks this lease as faulted. The pool will discard the underlying
    /// browser rather than returning it to circulation.
    /// </summary>
    void Invalidate();
}

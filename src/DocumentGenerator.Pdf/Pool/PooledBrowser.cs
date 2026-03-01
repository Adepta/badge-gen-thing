using PuppeteerSharp;

namespace DocumentGenerator.Pdf.Pool;

/// <summary>
/// Internal wrapper that tracks per-instance metadata for pool management.
/// </summary>
internal sealed class PooledBrowser
{
    /// <summary>The underlying PuppeteerSharp browser instance.</summary>
    public IBrowser Browser { get; }

    /// <summary>Total number of renders this instance has completed.</summary>
    public int RenderCount { get; private set; }

    /// <summary>UTC time the browser was last returned to the idle queue.</summary>
    public DateTimeOffset LastReturnedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Wraps <paramref name="browser"/> and resets per-instance counters.
    /// </summary>
    /// <param name="browser">The newly launched Chromium browser instance.</param>
    public PooledBrowser(IBrowser browser) => Browser = browser;

    /// <summary>Increments <see cref="RenderCount"/> by one after a successful render.</summary>
    public void IncrementRenderCount() => RenderCount++;
}

using PuppeteerSharp;

namespace DocumentGenerator.Pdf.Pool;

/// <summary>
/// Internal wrapper that tracks per-instance metadata for pool management.
/// </summary>
internal sealed class PooledBrowser
{
    public IBrowser Browser { get; }

    /// <summary>Total number of renders this instance has completed.</summary>
    public int RenderCount { get; private set; }

    /// <summary>UTC time the browser was last returned to the idle queue.</summary>
    public DateTimeOffset LastReturnedAt { get; set; } = DateTimeOffset.UtcNow;

    public PooledBrowser(IBrowser browser) => Browser = browser;

    public void IncrementRenderCount() => RenderCount++;
}

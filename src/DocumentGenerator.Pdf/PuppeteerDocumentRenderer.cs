using DocumentGenerator.Core.Interfaces;
using DocumentGenerator.Core.Models;
using Microsoft.Extensions.Logging;
using PuppeteerSharp;
using PuppeteerSharp.Media;
using CorePdfOptions = DocumentGenerator.Core.Models.PdfOptions;

namespace DocumentGenerator.Pdf;

/// <summary>
/// Renders HTML to PDF using a leased Chromium instance from the pool.
/// Each render opens a new Page (tab) within the browser, which is cheap
/// compared to launching a full browser process.
/// </summary>
public sealed class PuppeteerDocumentRenderer : IDocumentRenderer
{
    private readonly IBrowserPool<IBrowser> _pool;
    private readonly ILogger<PuppeteerDocumentRenderer> _logger;

    /// <summary>
    /// Initialises the renderer with its pool and logger.
    /// </summary>
    /// <param name="pool">The Chromium browser pool to lease instances from.</param>
    /// <param name="logger">Logger for render lifecycle events.</param>
    public PuppeteerDocumentRenderer(
        IBrowserPool<IBrowser> pool,
        ILogger<PuppeteerDocumentRenderer> logger)
    {
        _pool   = pool;
        _logger = logger;
    }

    /// <summary>
    /// Renders the supplied HTML string to a PDF byte array using a leased Chromium instance.
    /// A new browser tab (page) is opened for each render and closed when done.
    /// </summary>
    /// <param name="html">Fully rendered HTML document string.</param>
    /// <param name="options">PDF output options (format, margins, orientation, etc.).</param>
    /// <param name="cancellationToken">Token to cancel the render.</param>
    /// <returns>Raw PDF bytes.</returns>
    public async Task<byte[]> RenderPdfAsync(
        string html,
        CorePdfOptions options,
        CancellationToken cancellationToken = default)
    {
        await using var lease = await _pool.AcquireAsync(cancellationToken);

        IPage? page = null;
        try
        {
            page = await lease.Browser.NewPageAsync();

            // Load HTML directly — avoids file I/O and works in containers
            await page.SetContentAsync(html, new NavigationOptions
            {
                WaitUntil = [WaitUntilNavigation.Networkidle0],
                Timeout = 30_000
            });

            var pdfOptions = MapOptions(options);
            var pdfBytes = await page.PdfDataAsync(pdfOptions);

            _logger.LogDebug("PDF rendered — {Bytes:N0} bytes", pdfBytes.Length);
            return pdfBytes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PDF render failed, invalidating browser lease");
            lease.Invalidate();
            throw;
        }
        finally
        {
            if (page is not null)
            {
                try { await page.CloseAsync(); }
                catch { /* best-effort */ }
            }
        }
    }

    // -------------------------------------------------------------------------
    // Option mapping
    // -------------------------------------------------------------------------

    private static PuppeteerSharp.PdfOptions MapOptions(CorePdfOptions src)
    {
        var opts = new PuppeteerSharp.PdfOptions
        {
            Landscape       = src.Landscape,
            PrintBackground = src.PrintBackground,
            Scale           = (decimal)src.Scale,
            DisplayHeaderFooter = src.HeaderTemplate is not null || src.FooterTemplate is not null,
            HeaderTemplate  = src.HeaderTemplate ?? "<span></span>",
            FooterTemplate  = src.FooterTemplate ?? "<span></span>"
        };

        // Custom dimensions override named format — enables credit-card/badge sized PDFs
        if (!string.IsNullOrWhiteSpace(src.Width) && !string.IsNullOrWhiteSpace(src.Height))
        {
            opts.Width  = src.Width;
            opts.Height = src.Height;
        }
        else
        {
            opts.Format = src.Format.ToUpperInvariant() switch
            {
                "A4"      => PaperFormat.A4,
                "A3"      => PaperFormat.A3,
                "A2"      => PaperFormat.A2,
                "LETTER"  => PaperFormat.Letter,
                "LEGAL"   => PaperFormat.Legal,
                "TABLOID" => PaperFormat.Tabloid,
                _         => PaperFormat.A4
            };
        }

        if (src.Margins is not null)
        {
            opts.MarginOptions = new MarginOptions
            {
                Top    = src.Margins.Top,
                Bottom = src.Margins.Bottom,
                Left   = src.Margins.Left,
                Right  = src.Margins.Right
            };
        }

        return opts;
    }
}

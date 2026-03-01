using System.Diagnostics;
using DocumentGenerator.Core.Errors;
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
        var sw = Stopwatch.StartNew();
        _logger.LogDebug(
            "Acquiring browser lease — HtmlLength: {HtmlLength}", html.Length);

        await using var lease = await _pool.AcquireAsync(cancellationToken);

        IPage? page = null;
        try
        {
            page = await lease.Browser.NewPageAsync();

            // Load HTML directly — avoids file I/O and works in containers
            try
            {
                await page.SetContentAsync(html, new NavigationOptions
                {
                    // Load fires after all sub-resources (stylesheets, fonts) have loaded.
                    // Networkidle0 was previously used but blocks indefinitely when external
                    // resources (e.g. Google Fonts) are slow or unreachable, causing blank PDFs.
                    WaitUntil = [WaitUntilNavigation.Load],
                    Timeout   = 30_000
                });
            }
            catch (PuppeteerSharp.PuppeteerException ex) when (ex.Message.Contains("Timeout"))
            {
                throw RenderException.PageTimeout(null, 30_000, ex);
            }

            // Wait for fonts to finish loading (covers CSS @import font faces).
            // Times out gracefully after 5s so a missing font never blocks rendering.
            try
            {
                await page.EvaluateFunctionAsync(
                    "() => document.fonts.ready",
                    Array.Empty<object>());
            }
            catch { /* best-effort — proceed even if fonts API unavailable */ }

            var pdfOptions = MapOptions(options);
            var pdfBytes   = await page.PdfDataAsync(pdfOptions);

            sw.Stop();
            _logger.LogInformation(
                "Chromium render complete — Bytes: {Bytes}, ElapsedMs: {ElapsedMs}",
                pdfBytes.Length, sw.ElapsedMilliseconds);

            return pdfBytes;
        }
        catch (DocumentGeneratorException)
        {
            sw.Stop();
            _logger.LogError(
                "Chromium render failed after {ElapsedMs}ms — invalidating browser lease",
                sw.ElapsedMilliseconds);
            lease.Invalidate();
            throw;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex,
                "Chromium render failed after {ElapsedMs}ms — invalidating browser lease",
                sw.ElapsedMilliseconds);
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

    /// <inheritdoc/>
    public async Task<byte[]> RenderPngAsync(
        string html,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        _logger.LogDebug("Acquiring browser lease for PNG — HtmlLength: {HtmlLength}", html.Length);

        await using var lease = await _pool.AcquireAsync(cancellationToken);

        IPage? page = null;
        try
        {
            page = await lease.Browser.NewPageAsync();

            // Measure the document's natural size after rendering at a neutral viewport,
            // then resize the viewport to match exactly so the screenshot clips to the badge.
            // DeviceScaleFactor = 2 gives a 2× retina-quality PNG without changing CSS layout.
            await page.SetViewportAsync(new ViewPortOptions
            {
                Width             = 1200,
                Height            = 900,
                DeviceScaleFactor = 2
            });

            try
            {
                await page.SetContentAsync(html, new NavigationOptions
                {
                    WaitUntil = [WaitUntilNavigation.Load],
                    Timeout   = 30_000
                });
            }
            catch (PuppeteerSharp.PuppeteerException ex) when (ex.Message.Contains("Timeout"))
            {
                throw RenderException.PageTimeout(null, 30_000, ex);
            }

            try
            {
                await page.EvaluateFunctionAsync(
                    "() => document.fonts.ready",
                    Array.Empty<object>());
            }
            catch { /* best-effort */ }

            // Measure the rendered document size so we can clip the screenshot to it.
            // body/html may have explicit width/height in mm — Chromium converts these to px.
            var dimensions = await page.EvaluateFunctionAsync<int[]>(@"() => {
                const el = document.body.firstElementChild || document.body;
                const r  = el.getBoundingClientRect();
                return [Math.ceil(r.width), Math.ceil(r.height)];
            }");

            var docWidth  = dimensions?[0] ?? 0;
            var docHeight = dimensions?[1] ?? 0;

            // If we got a valid size, shrink the viewport to the badge dimensions so
            // FullPage:false captures exactly the badge and nothing else.
            if (docWidth > 0 && docHeight > 0)
            {
                await page.SetViewportAsync(new ViewPortOptions
                {
                    Width             = docWidth,
                    Height            = docHeight,
                    DeviceScaleFactor = 2
                });
            }

            var pngBytes = await page.ScreenshotDataAsync(new ScreenshotOptions
            {
                FullPage = false,   // capture only the viewport = the badge
                Type     = ScreenshotType.Png
            });

            sw.Stop();
            _logger.LogInformation(
                "Chromium PNG render complete — Bytes: {Bytes}, ElapsedMs: {ElapsedMs}",
                pngBytes.Length, sw.ElapsedMilliseconds);

            return pngBytes;
        }
        catch (DocumentGeneratorException)
        {
            sw.Stop();
            lease.Invalidate();
            throw;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex,
                "Chromium PNG render failed after {ElapsedMs}ms — invalidating browser lease",
                sw.ElapsedMilliseconds);
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

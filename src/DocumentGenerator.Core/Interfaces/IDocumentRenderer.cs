using DocumentGenerator.Core.Models;

namespace DocumentGenerator.Core.Interfaces;

/// <summary>Output format requested from the renderer.</summary>
public enum OutputFormat
{
    /// <summary>PDF document (default).</summary>
    Pdf,
    /// <summary>Full-page PNG screenshot.</summary>
    Png
}

/// <summary>
/// Converts a fully-rendered HTML string into bytes (PDF or PNG).
/// The implementation is expected to be backed by a pooled Chromium instance.
/// </summary>
public interface IDocumentRenderer
{
    /// <summary>
    /// Renders <paramref name="html"/> to PDF bytes using the configured
    /// PDF options and returns the result.
    /// </summary>
    /// <param name="html">Fully-rendered HTML string to load into Chromium.</param>
    /// <param name="options">Paper size, margins, and other PDF output settings.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>Raw PDF bytes ready to stream or persist.</returns>
    Task<byte[]> RenderPdfAsync(string html, PdfOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Renders <paramref name="html"/> to a full-page PNG screenshot and returns the bytes.
    /// </summary>
    /// <param name="html">Fully-rendered HTML string to load into Chromium.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>Raw PNG bytes.</returns>
    Task<byte[]> RenderPngAsync(string html, CancellationToken cancellationToken = default);
}

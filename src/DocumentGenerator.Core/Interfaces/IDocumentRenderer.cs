using DocumentGenerator.Core.Models;

namespace DocumentGenerator.Core.Interfaces;

/// <summary>
/// Converts a fully-rendered HTML string into a PDF byte array.
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
    /// <exception cref="OperationCanceledException">Thrown if <paramref name="cancellationToken"/> is cancelled.</exception>
    Task<byte[]> RenderPdfAsync(string html, PdfOptions options, CancellationToken cancellationToken = default);
}

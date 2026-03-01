namespace DocumentGenerator.Core.Models;

/// <summary>
/// The output of a completed render job.
/// </summary>
public sealed class RenderResult
{
    /// <summary>Matches the originating <see cref="RenderRequest.JobId"/>.</summary>
    public Guid JobId { get; init; }

    /// <summary>The raw PDF bytes ready to stream or persist.</summary>
    public required byte[] PdfBytes { get; init; }

    /// <summary>Duration of the full render pipeline.</summary>
    public TimeSpan ElapsedTime { get; init; }

    /// <summary>Document type from the source template.</summary>
    public string DocumentType { get; init; } = string.Empty;

    /// <summary>
    /// Creates a successful <see cref="RenderResult"/> with the given render output.
    /// </summary>
    /// <param name="jobId">Job identifier, matches the originating <see cref="RenderRequest.JobId"/>.</param>
    /// <param name="pdfBytes">The rendered PDF (or PNG) bytes.</param>
    /// <param name="elapsed">Wall-clock time taken by the full pipeline.</param>
    /// <param name="documentType">The document type string from the template.</param>
    /// <returns>A populated <see cref="RenderResult"/> instance.</returns>
    public static RenderResult Success(Guid jobId, byte[] pdfBytes, TimeSpan elapsed, string documentType) =>
        new()
        {
            JobId = jobId,
            PdfBytes = pdfBytes,
            ElapsedTime = elapsed,
            DocumentType = documentType
        };
}

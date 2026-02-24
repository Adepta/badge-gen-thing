using DocumentGenerator.Core.Models;

namespace DocumentGenerator.Core.Interfaces;

/// <summary>
/// Orchestrates the full document generation pipeline:
/// template rendering → PDF conversion.
/// This is the primary entry point for all callers (console, API, queue consumer).
/// </summary>
public interface IDocumentPipeline
{
    /// <summary>
    /// Executes a full render job and returns the PDF result.
    /// </summary>
    /// <param name="request">The render job, including the template and job ID.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A <see cref="RenderResult"/> containing the PDF bytes and timing metadata.</returns>
    /// <exception cref="OperationCanceledException">Thrown if <paramref name="cancellationToken"/> is cancelled.</exception>
    Task<RenderResult> ExecuteAsync(RenderRequest request, CancellationToken cancellationToken = default);
}

namespace DocumentGenerator.Core.Errors;

/// <summary>
/// Thrown when the document render pipeline fails.
/// </summary>
public sealed class RenderException : DocumentGeneratorException
{
    /// <summary>The correlation / job ID associated with the failed render, if known.</summary>
    public Guid? CorrelationId { get; }

    private RenderException(
        ErrorCode code,
        string message,
        Guid? correlationId,
        Exception? inner = null)
        : base(
            code,
            message,
            BuildContext(correlationId),
            inner!)
    {
        CorrelationId = correlationId;
    }

    // ── Factory methods ───────────────────────────────────────────────────────

    /// <summary>
    /// Creates a <see cref="ErrorCode.PipelineFailed"/> exception wrapping an unexpected error.
    /// </summary>
    public static RenderException PipelineFailed(Guid? correlationId, Exception inner) =>
        new(ErrorCode.PipelineFailed,
            $"Document pipeline failed for job '{correlationId}'.",
            correlationId, inner);

    /// <summary>
    /// Creates a <see cref="ErrorCode.RenderEmptyOutput"/> exception when Chromium
    /// returns zero bytes.
    /// </summary>
    public static RenderException EmptyOutput(Guid? correlationId) =>
        new(ErrorCode.RenderEmptyOutput,
            $"Chromium returned zero PDF bytes for job '{correlationId}'.",
            correlationId);

    /// <summary>
    /// Creates a <see cref="ErrorCode.RenderPageTimeout"/> exception when Chromium
    /// times out loading the HTML page.
    /// </summary>
    public static RenderException PageTimeout(Guid? correlationId, int timeoutMs, Exception inner) =>
        new(ErrorCode.RenderPageTimeout,
            $"Chromium page load timed out after {timeoutMs}ms for job '{correlationId}'.",
            correlationId, inner);

    // ─────────────────────────────────────────────────────────────────────────

    private static IReadOnlyDictionary<string, object?> BuildContext(Guid? correlationId) =>
        new Dictionary<string, object?> { ["correlationId"] = correlationId?.ToString() };
}

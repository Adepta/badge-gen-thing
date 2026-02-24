namespace DocumentGenerator.Core.Interfaces;

/// <summary>
/// Abstraction for recording render outcome metrics.
/// The Console project registers <c>RenderStats</c> as the implementation;
/// other hosts (tests, file-worker) can register a no-op or substitute.
/// </summary>
public interface IRenderMetrics
{
    /// <summary>Records a successful render completion.</summary>
    void RecordSuccess();

    /// <summary>Records a failed render attempt.</summary>
    void RecordFailure();
}

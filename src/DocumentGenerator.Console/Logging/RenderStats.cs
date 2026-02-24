using System.Diagnostics;
using DocumentGenerator.Core.Interfaces;

namespace DocumentGenerator.Console.Logging;

/// <summary>
/// Thread-safe counters for the TUI status bar.
/// Injected as a singleton so both <see cref="TuiRenderer"/> (reads)
/// and <c>DocumentRenderRequestHandler</c> (writes) share the same instance.
/// </summary>
public sealed class RenderStats : IRenderMetrics
{
    private long _successCount;
    private long _failureCount;
    private readonly Stopwatch _uptime = Stopwatch.StartNew();

    /// <summary>Total successfully completed renders since service start.</summary>
    public long SuccessCount => Interlocked.Read(ref _successCount);

    /// <summary>Total failed renders since service start.</summary>
    public long FailureCount => Interlocked.Read(ref _failureCount);

    /// <summary>Time elapsed since the service started.</summary>
    public TimeSpan Uptime => _uptime.Elapsed;

    /// <summary>Increments the success counter by one.</summary>
    public void RecordSuccess() => Interlocked.Increment(ref _successCount);

    /// <summary>Increments the failure counter by one.</summary>
    public void RecordFailure() => Interlocked.Increment(ref _failureCount);
}

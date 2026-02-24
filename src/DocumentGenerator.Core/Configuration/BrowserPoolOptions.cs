namespace DocumentGenerator.Core.Configuration;

/// <summary>
/// Each Chromium instance holds ~150 MB of RAM. Keep <see cref="MaxSize"/> in sync with
/// the Rebus worker count (<c>KafkaOptions.MaxConcurrentRenders</c>) so the pool is never
/// the bottleneck and never starved.
/// </summary>
public sealed class BrowserPoolOptions
{
    public const string SectionName = "BrowserPool";

    /// <summary>
    /// Minimum number of browser instances to keep warm at startup.
    /// Defaults to 1 so the first request is fast.
    /// </summary>
    public int MinSize { get; init; } = 1;

    /// <summary>
    /// Maximum number of concurrent browser instances the pool will launch.
    /// Set this based on available RAM — each Chromium instance uses ~100-200 MB.
    /// Defaults to 4.
    /// </summary>
    public int MaxSize { get; init; } = 4;

    /// <summary>
    /// Maximum time to wait for an available browser before throwing.
    /// Defaults to 30 seconds.
    /// </summary>
    public TimeSpan AcquireTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// How long an idle browser instance is kept alive before being reaped.
    /// Null means instances are kept indefinitely.
    /// Defaults to 5 minutes.
    /// </summary>
    public TimeSpan? IdleTimeout { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Maximum number of renders a single browser instance performs before
    /// it is recycled. This prevents memory leaks from long-lived Chromium processes.
    /// Defaults to 100. Set to 0 to disable.
    /// </summary>
    public int MaxRendersPerInstance { get; init; } = 100;
}

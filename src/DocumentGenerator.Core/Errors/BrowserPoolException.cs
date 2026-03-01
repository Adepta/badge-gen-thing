namespace DocumentGenerator.Core.Errors;

/// <summary>
/// Thrown when the Chromium browser pool cannot fulfil a request.
/// </summary>
public sealed class BrowserPoolException : DocumentGeneratorException
{
    private BrowserPoolException(
        ErrorCode code,
        string message,
        Exception? inner = null)
        : base(code, message, inner!) { }

    // ── Factory methods ───────────────────────────────────────────────────────

    /// <summary>
    /// Creates a <see cref="ErrorCode.BrowserPoolTimeout"/> exception when no browser
    /// becomes available within the configured acquire timeout.
    /// </summary>
    public static BrowserPoolException AcquireTimeout(TimeSpan timeout, int poolSize, int active) =>
        new(ErrorCode.BrowserPoolTimeout,
            $"Could not acquire a browser from the pool within {timeout.TotalSeconds}s " +
            $"(pool size: {poolSize}, active: {active}).");

    /// <summary>
    /// Creates a <see cref="ErrorCode.BrowserLaunchFailed"/> exception when Chromium
    /// cannot be started.
    /// </summary>
    public static BrowserPoolException LaunchFailed(Exception inner) =>
        new(ErrorCode.BrowserLaunchFailed,
            "Failed to launch a Chromium browser instance.", inner);

    /// <summary>
    /// Creates a <see cref="ErrorCode.BrowserDisconnected"/> exception when an in-use
    /// browser instance disconnects unexpectedly.
    /// </summary>
    public static BrowserPoolException Disconnected() =>
        new(ErrorCode.BrowserDisconnected,
            "The Chromium browser instance disconnected unexpectedly during the render.");

    /// <summary>
    /// Creates a <see cref="ErrorCode.BrowserPoolDisposed"/> exception when the pool
    /// has already been shut down.
    /// </summary>
    public static BrowserPoolException Disposed() =>
        new(ErrorCode.BrowserPoolDisposed,
            "The browser pool has been disposed and cannot accept new requests.");
}

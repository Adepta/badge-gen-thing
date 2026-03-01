namespace DocumentGenerator.Core.Errors;

/// <summary>
/// Canonical error codes used throughout the document generation pipeline.
///
/// Format: [Category][3-digit sequence]
///   DG1xx — Template errors
///   DG2xx — Render/pipeline errors
///   DG3xx — Browser pool errors
///   DG4xx — Messaging/broker errors
///   DG5xx — Printing errors
///   DG6xx — Configuration errors
///   DG9xx — Unexpected/unclassified errors
/// </summary>
public enum ErrorCode
{
    // ── DG1xx — Template ─────────────────────────────────────────────────────

    /// <summary>The requested template name does not exist in the templates directory.</summary>
    TemplateNotFound = 1001,

    /// <summary>The template HTML file exists but could not be read (permissions, locked, corrupt).</summary>
    TemplateReadFailed = 1002,

    /// <summary>The template name supplied by the caller is null, empty, or contains invalid characters.</summary>
    TemplateNameInvalid = 1003,

    /// <summary>The Handlebars template contains a syntax error and could not be compiled.</summary>
    TemplateCompileFailed = 1004,

    /// <summary>Handlebars rendering failed at runtime (missing partial, helper threw, etc.).</summary>
    TemplateRenderFailed = 1005,

    // ── DG2xx — Render / pipeline ────────────────────────────────────────────

    /// <summary>The document pipeline failed with an unrecoverable error.</summary>
    PipelineFailed = 2001,

    /// <summary>Chromium loaded the HTML but PDF generation returned zero bytes.</summary>
    RenderEmptyOutput = 2002,

    /// <summary>The render operation was cancelled before it could complete.</summary>
    RenderCancelled = 2003,

    /// <summary>Chromium timed out loading the page before the render could proceed.</summary>
    RenderPageTimeout = 2004,

    // ── DG3xx — Browser pool ──────────────────────────────────────────────────

    /// <summary>No browser became available within the configured acquire timeout.</summary>
    BrowserPoolTimeout = 3001,

    /// <summary>Chromium failed to launch (binary missing, sandbox issue, permissions).</summary>
    BrowserLaunchFailed = 3002,

    /// <summary>
    /// The browser instance disconnected unexpectedly during a render
    /// (crashed, OOM-killed, etc.).
    /// </summary>
    BrowserDisconnected = 3003,

    /// <summary>The browser pool has been disposed and can no longer accept requests.</summary>
    BrowserPoolDisposed = 3004,

    // ── DG4xx — Messaging / broker ───────────────────────────────────────────

    /// <summary>Publishing a render request to Kafka failed.</summary>
    BrokerPublishFailed = 4001,

    /// <summary>
    /// The API did not receive a render result from the Console within the
    /// configured timeout, returning HTTP 504 to the caller.
    /// </summary>
    BrokerResultTimeout = 4002,

    /// <summary>A Kafka message could not be deserialized into the expected message type.</summary>
    BrokerDeserializeFailed = 4003,

    /// <summary>
    /// The render request exceeded the maximum retry count and was moved
    /// to the dead-letter topic.
    /// </summary>
    BrokerDeadLettered = 4004,

    // ── DG5xx — Printing ─────────────────────────────────────────────────────

    /// <summary>The cloud API call from the Bridge failed (network error, non-2xx, empty body).</summary>
    CloudRenderFailed = 5001,

    /// <summary>The Base64 payload returned by the cloud API could not be decoded.</summary>
    CloudResponseDecodeFailed = 5002,

    /// <summary>The local print spooler rejected or failed the print job.</summary>
    PrintSpoolerFailed = 5003,

    /// <summary>The print process (SumatraPDF / Edge) did not exit within the allowed timeout.</summary>
    PrintProcessTimeout = 5004,

    /// <summary>No suitable PDF viewer / print helper was found on the local machine.</summary>
    PrintHelperNotFound = 5005,

    // ── DG6xx — Configuration ────────────────────────────────────────────────

    /// <summary>A required configuration value is missing or empty.</summary>
    ConfigurationMissing = 6001,

    /// <summary>A configuration value is present but invalid (wrong type, out of range, etc.).</summary>
    ConfigurationInvalid = 6002,

    // ── DG9xx — Unexpected ────────────────────────────────────────────────────

    /// <summary>An unexpected error occurred that does not fit any other category.</summary>
    Unexpected = 9001,
}

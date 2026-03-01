namespace DocumentGenerator.Bridge.Models;

/// <summary>
/// Response returned to the iPad by both <c>POST /print</c> and <c>POST /render</c>.
/// </summary>
public sealed class PrintResponse
{
    /// <summary>
    /// Echoes the <c>CorrelationId</c> from the request (or a bridge-generated one).
    /// </summary>
    public Guid CorrelationId { get; init; }

    /// <summary><c>true</c> when the cloud render (and optional local print) succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>
    /// Base64-encoded PDF or PNG bytes of the rendered badge.
    /// Always present on success so the iPad can show a preview or save the badge.
    /// </summary>
    public string? DocumentBase64 { get; init; }

    /// <summary>
    /// MIME type of <see cref="DocumentBase64"/>: <c>application/pdf</c> or <c>image/png</c>.
    /// </summary>
    public string? MimeType { get; init; }

    /// <summary>
    /// Name of the local printer the job was sent to.
    /// <c>null</c> when called via <c>POST /render</c> (render-only, no print).
    /// </summary>
    public string? PrinterUsed { get; init; }

    /// <summary>
    /// <c>true</c> when the document was successfully submitted to the local print spooler.
    /// <c>null</c> when called via <c>POST /render</c>.
    /// </summary>
    public bool? Printed { get; init; }

    /// <summary>Total time from iPad request to bridge response.</summary>
    public TimeSpan ElapsedTime { get; init; }

    /// <summary>UTC timestamp when the bridge completed processing.</summary>
    public DateTimeOffset CompletedAt { get; init; }

    /// <summary>Human-readable error. Populated only when <see cref="Success"/> is <c>false</c>.</summary>
    public string? Error { get; init; }

    /// <summary>
    /// Machine-readable error code (e.g. <c>DG5001</c>) when <see cref="Success"/> is <c>false</c>.
    /// Allows iPad clients to distinguish error categories without parsing <see cref="Error"/>.
    /// </summary>
    public string? ErrorCode { get; init; }

    /// <summary>Creates a successful render-only response (no print).</summary>
    public static PrintResponse RenderOk(
        Guid correlationId,
        string documentBase64,
        string mimeType,
        TimeSpan elapsed) => new()
        {
            CorrelationId = correlationId,
            Success       = true,
            DocumentBase64 = documentBase64,
            MimeType      = mimeType,
            Printed       = null,
            ElapsedTime   = elapsed,
            CompletedAt   = DateTimeOffset.UtcNow
        };

    /// <summary>Creates a successful print response.</summary>
    public static PrintResponse PrintOk(
        Guid correlationId,
        string documentBase64,
        string mimeType,
        string printerUsed,
        TimeSpan elapsed) => new()
        {
            CorrelationId  = correlationId,
            Success        = true,
            DocumentBase64 = documentBase64,
            MimeType       = mimeType,
            PrinterUsed    = printerUsed,
            Printed        = true,
            ElapsedTime    = elapsed,
            CompletedAt    = DateTimeOffset.UtcNow
        };

    /// <summary>Creates a failure response with optional machine-readable error code.</summary>
    public static PrintResponse Fail(
        Guid correlationId,
        string error,
        TimeSpan elapsed,
        string? errorCode = null) => new()
        {
            CorrelationId = correlationId,
            Success       = false,
            Error         = error,
            ErrorCode     = errorCode,
            ElapsedTime   = elapsed,
            CompletedAt   = DateTimeOffset.UtcNow
        };
}

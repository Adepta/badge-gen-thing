namespace DocumentGenerator.Api.Models;

/// <summary>
/// Response body returned from POST /api/badges/render.
/// On success, <see cref="DocumentBase64"/> contains the rendered badge encoded as Base64.
/// </summary>
public sealed class BadgeRenderResponse
{
    /// <summary>
    /// Echoes the <c>CorrelationId</c> supplied in the request (or a server-generated one).
    /// The bridge uses this to match the response back to the originating iPad request.
    /// </summary>
    public Guid CorrelationId { get; init; }

    /// <summary>
    /// Unique server-side job identifier — useful for distributed tracing and support queries.
    /// </summary>
    public Guid JobId { get; init; }

    /// <summary>
    /// <c>true</c> when the badge was rendered successfully.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Base64-encoded PDF or PNG bytes of the rendered badge.
    /// Populated only when <see cref="Success"/> is <c>true</c>.
    /// </summary>
    public string? DocumentBase64 { get; init; }

    /// <summary>
    /// MIME type of the encoded document: <c>application/pdf</c> or <c>image/png</c>.
    /// </summary>
    public string? MimeType { get; init; }

    /// <summary>
    /// The document type resolved from the template, e.g. "badge".
    /// </summary>
    public string? DocumentType { get; init; }

    /// <summary>
    /// Server-side render duration. Useful for performance monitoring.
    /// </summary>
    public TimeSpan ElapsedTime { get; init; }

    /// <summary>
    /// UTC timestamp when rendering completed.
    /// </summary>
    public DateTimeOffset CompletedAt { get; init; }

    /// <summary>
    /// Human-readable error description. Populated only when <see cref="Success"/> is <c>false</c>.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Creates a successful response containing the rendered document.
    /// </summary>
    public static BadgeRenderResponse Ok(
        Guid correlationId,
        Guid jobId,
        byte[] documentBytes,
        string mimeType,
        string documentType,
        TimeSpan elapsed) => new()
        {
            CorrelationId = correlationId,
            JobId = jobId,
            Success = true,
            DocumentBase64 = Convert.ToBase64String(documentBytes),
            MimeType = mimeType,
            DocumentType = documentType,
            ElapsedTime = elapsed,
            CompletedAt = DateTimeOffset.UtcNow
        };

    /// <summary>
    /// Creates a failure response with an error message.
    /// </summary>
    public static BadgeRenderResponse Fail(Guid correlationId, string error) => new()
    {
        CorrelationId = correlationId,
        JobId = Guid.Empty,
        Success = false,
        Error = error,
        CompletedAt = DateTimeOffset.UtcNow
    };
}

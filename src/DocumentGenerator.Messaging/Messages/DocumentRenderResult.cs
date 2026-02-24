namespace DocumentGenerator.Messaging.Messages;

/// <summary>
/// Outbound Kafka message published by the generator after a render completes
/// (successfully or otherwise).
///
/// Topic: render.results
/// Key:   CorrelationId (iPad filters on this to find its own response)
/// </summary>
public sealed class DocumentRenderResult
{
    /// <summary>Matches <see cref="DocumentRenderRequest.CorrelationId"/>.</summary>
    public Guid CorrelationId { get; init; }

    /// <summary>Echoed from the originating request for easy routing on the iPad.</summary>
    public string DeviceId { get; init; } = string.Empty;

    /// <summary>Echoed from the originating request.</summary>
    public string? SessionId { get; init; }

    /// <summary>Whether the render succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>
    /// Base64-encoded PDF bytes. Null when <see cref="Success"/> is false.
    /// The iPad decodes this and hands it to the AirPrint / printing stack.
    /// </summary>
    public string? PdfBase64 { get; init; }

    /// <summary>Human-readable error description when <see cref="Success"/> is false.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Document type from the original template, e.g. "badge".</summary>
    public string DocumentType { get; init; } = string.Empty;

    /// <summary>How long the full render pipeline took.</summary>
    public TimeSpan ElapsedTime { get; init; }

    /// <summary>UTC timestamp when the result was published.</summary>
    public DateTimeOffset CompletedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Creates a successful <see cref="DocumentRenderResult"/> from rendered PDF bytes.
    /// The bytes are automatically Base64-encoded for Kafka transport.
    /// </summary>
    /// <param name="correlationId">Correlation ID echoed from the request.</param>
    /// <param name="deviceId">Device ID echoed from the request.</param>
    /// <param name="sessionId">Session ID echoed from the request.</param>
    /// <param name="documentType">Document type label from the template.</param>
    /// <param name="pdfBytes">Raw PDF bytes to encode and include in the result.</param>
    /// <param name="elapsed">Time taken by the full render pipeline.</param>
    /// <returns>A populated success result ready to publish.</returns>
    public static DocumentRenderResult Succeeded(
        Guid correlationId,
        string deviceId,
        string? sessionId,
        string documentType,
        byte[] pdfBytes,
        TimeSpan elapsed) => new()
        {
            CorrelationId = correlationId,
            DeviceId      = deviceId,
            SessionId     = sessionId,
            DocumentType  = documentType,
            Success       = true,
            PdfBase64     = Convert.ToBase64String(pdfBytes),
            ElapsedTime   = elapsed
        };

    /// <summary>
    /// Creates a failure <see cref="DocumentRenderResult"/> carrying the error description.
    /// </summary>
    /// <param name="correlationId">Correlation ID echoed from the request.</param>
    /// <param name="deviceId">Device ID echoed from the request.</param>
    /// <param name="sessionId">Session ID echoed from the request.</param>
    /// <param name="documentType">Document type label from the template.</param>
    /// <param name="errorMessage">Human-readable description of why the render failed.</param>
    /// <returns>A populated failure result ready to publish.</returns>
    public static DocumentRenderResult Failed(
        Guid correlationId,
        string deviceId,
        string? sessionId,
        string documentType,
        string errorMessage) => new()
        {
            CorrelationId = correlationId,
            DeviceId      = deviceId,
            SessionId     = sessionId,
            DocumentType  = documentType,
            Success       = false,
            ErrorMessage  = errorMessage
        };
}

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
    /// Base64-encoded PDF bytes. Populated when <see cref="DocumentRenderRequest.ReturnPdfInline"/>
    /// is <see langword="true"/> and the render succeeded. Null otherwise.
    /// The iPad decodes this and hands it to the AirPrint / printing stack.
    /// </summary>
    public string? PdfBase64 { get; init; }

    /// <summary>
    /// Absolute path to the saved PDF file on the shared network location.
    /// Populated when <see cref="DocumentRenderRequest.ReturnPdfInline"/> is
    /// <see langword="false"/> and the render succeeded. Null otherwise.
    /// </summary>
    public string? PdfPath { get; init; }

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
    /// </summary>
    /// <param name="correlationId">Matched from the originating request.</param>
    /// <param name="deviceId">Echoed from the originating request.</param>
    /// <param name="sessionId">Echoed from the originating request.</param>
    /// <param name="documentType">Document type from the template.</param>
    /// <param name="pdfBytes">Raw rendered PDF bytes.</param>
    /// <param name="elapsed">Total pipeline duration.</param>
    /// <param name="returnInline">
    /// When <see langword="true"/>, <paramref name="pdfBytes"/> are Base64-encoded into
    /// <see cref="PdfBase64"/>. When <see langword="false"/>, <see cref="PdfBase64"/> is
    /// null and <see cref="PdfPath"/> must be set by the caller.
    /// </param>
    /// <param name="pdfPath">
    /// Absolute path to the saved PDF file. Only used when <paramref name="returnInline"/>
    /// is <see langword="false"/>; ignored otherwise.
    /// </param>
    public static DocumentRenderResult Succeeded(
        Guid correlationId, string deviceId, string? sessionId,
        string documentType, byte[] pdfBytes, TimeSpan elapsed,
        bool returnInline = true, string? pdfPath = null) => new()
        {
            CorrelationId = correlationId,
            DeviceId      = deviceId,
            SessionId     = sessionId,
            DocumentType  = documentType,
            Success       = true,
            PdfBase64     = returnInline ? Convert.ToBase64String(pdfBytes) : null,
            PdfPath       = returnInline ? null : pdfPath,
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

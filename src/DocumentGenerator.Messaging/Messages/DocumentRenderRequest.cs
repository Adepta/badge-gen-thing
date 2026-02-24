using DocumentGenerator.Core.Models;

namespace DocumentGenerator.Messaging.Messages;

/// <summary>
/// Inbound Kafka message published by the iPad when a badge (or any document)
/// needs to be generated.
///
/// Topic: render.requests
/// Key:   CorrelationId (ensures ordering per-request within a partition)
/// </summary>
public sealed class DocumentRenderRequest
{
    /// <summary>
    /// Unique ID for this render job. The iPad sets this and uses it to
    /// match the response back to the correct attendee/screen.
    /// </summary>
    public Guid CorrelationId { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Identifies the specific iPad that sent the request, so the result
    /// can be routed back to the correct device.
    /// </summary>
    public string DeviceId { get; init; } = string.Empty;

    /// <summary>
    /// Optional session/event context, e.g. "conference-2026" or "check-in-gate-3".
    /// Useful for multi-tenant or multi-event deployments.
    /// </summary>
    public string? SessionId { get; init; }

    /// <summary>
    /// The document to render. For badge printing this will always have
    /// documentType = "badge".
    /// </summary>
    public required DocumentTemplate Template { get; init; }

    /// <summary>UTC timestamp when the iPad sent the request.</summary>
    public DateTimeOffset RequestedAt { get; init; } = DateTimeOffset.UtcNow;
}

namespace DocumentGenerator.Core.Errors;

/// <summary>
/// Thrown when a Kafka / messaging broker operation fails.
/// </summary>
public sealed class BrokerException : DocumentGeneratorException
{
    /// <summary>The correlation ID of the affected render request, if known.</summary>
    public Guid? CorrelationId { get; }

    private BrokerException(
        ErrorCode code,
        string message,
        Guid? correlationId,
        Exception? inner = null)
        : base(
            code,
            message,
            BuildContext(correlationId),
            inner!)
    {
        CorrelationId = correlationId;
    }

    // ── Factory methods ───────────────────────────────────────────────────────

    /// <summary>
    /// Creates a <see cref="ErrorCode.BrokerPublishFailed"/> exception when publishing
    /// a render request to Kafka fails.
    /// </summary>
    public static BrokerException PublishFailed(Guid correlationId, Exception inner) =>
        new(ErrorCode.BrokerPublishFailed,
            $"Failed to publish render request to Kafka for CorrelationId '{correlationId}'.",
            correlationId, inner);

    /// <summary>
    /// Creates a <see cref="ErrorCode.BrokerResultTimeout"/> exception when the API
    /// does not receive a result within the configured timeout.
    /// </summary>
    public static BrokerException ResultTimeout(Guid correlationId, int timeoutSeconds) =>
        new(ErrorCode.BrokerResultTimeout,
            $"Render result not received within {timeoutSeconds}s for CorrelationId '{correlationId}'.",
            correlationId);

    /// <summary>
    /// Creates a <see cref="ErrorCode.BrokerDeadLettered"/> exception when a message
    /// exhausts all retries.
    /// </summary>
    public static BrokerException DeadLettered(Guid correlationId, int maxRetries) =>
        new(ErrorCode.BrokerDeadLettered,
            $"Render request for CorrelationId '{correlationId}' exceeded {maxRetries} retries " +
            "and was moved to the dead-letter topic.",
            correlationId);

    // ─────────────────────────────────────────────────────────────────────────

    private static IReadOnlyDictionary<string, object?> BuildContext(Guid? correlationId) =>
        new Dictionary<string, object?> { ["correlationId"] = correlationId?.ToString() };
}

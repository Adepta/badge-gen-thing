namespace DocumentGenerator.Messaging.Configuration;

/// <summary>
/// Keep <see cref="MaxConcurrentRenders"/> at or below
/// <see cref="DocumentGenerator.Core.Configuration.BrowserPoolOptions.MaxSize"/>
/// — Rebus worker threads map 1:1 onto pool slots.
/// </summary>
public sealed class KafkaOptions
{
    public const string SectionName = "Kafka";

    /// <summary>Comma-separated list of bootstrap brokers, e.g. "localhost:9092".</summary>
    public string BootstrapServers { get; init; } = "localhost:9092";

    /// <summary>
    /// Consumer group ID. All instances of the document generator sharing the same
    /// group will load-balance partitions between them — giving you horizontal scale
    /// without duplicate processing.
    /// </summary>
    public string ConsumerGroupId { get; init; } = "document-generator";

    /// <summary>Topic the iPads publish render requests to.</summary>
    public string RequestTopic { get; init; } = "render.requests";

    /// <summary>Topic the generator publishes completed results to, consumed by iPads.</summary>
    public string ResultTopic { get; init; } = "render.results";

    /// <summary>
    /// Topic for dead-letter messages — requests that failed after all retries.
    /// The iPad can monitor this for error handling / user feedback.
    /// </summary>
    public string DeadLetterTopic { get; init; } = "render.deadletter";

    /// <summary>
    /// Maximum number of times a failed render is retried before the message
    /// is sent to the dead-letter topic. Defaults to 3.
    /// </summary>
    public int MaxRetries { get; init; } = 3;

    /// <summary>
    /// Delay between retry attempts. Defaults to 2 seconds.
    /// Uses exponential backoff: attempt N waits RetryDelay * 2^(N-1).
    /// </summary>
    public TimeSpan RetryDelay { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// How long to wait for messages before looping. Keeps the consumer
    /// responsive to cancellation. Defaults to 1 second.
    /// </summary>
    public TimeSpan PollTimeout { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Maximum number of render requests processed concurrently.
    /// Should be kept at or below <see cref="Core.Configuration.BrowserPoolOptions.MaxSize"/>
    /// to avoid pool starvation. Defaults to 4.
    /// </summary>
    public int MaxConcurrentRenders { get; init; } = 4;

    // -------------------------------------------------------------------------
    // Security (SASL/TLS for production Kafka clusters)
    // -------------------------------------------------------------------------

    /// <summary>Security protocol. Options: Plaintext, Ssl, SaslPlaintext, SaslSsl.</summary>
    public string SecurityProtocol { get; init; } = "Plaintext";

    /// <summary>SASL mechanism. Options: Plain, ScramSha256, ScramSha512, OAuthBearer.</summary>
    public string? SaslMechanism { get; init; }

    /// <summary>SASL username (for managed Kafka services e.g. Confluent Cloud).</summary>
    public string? SaslUsername { get; init; }

    /// <summary>SASL password.</summary>
    public string? SaslPassword { get; init; }
}

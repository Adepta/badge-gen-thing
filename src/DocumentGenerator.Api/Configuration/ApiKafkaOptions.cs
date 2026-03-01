namespace DocumentGenerator.Api.Configuration;

/// <summary>
/// API-specific Kafka settings that extend the shared transport configuration.
///
/// <para>
/// The base connection settings (bootstrap servers, topics, security) live in
/// <see cref="DocumentGenerator.Messaging.Configuration.KafkaOptions"/> and are bound
/// from the <c>Kafka</c> section. This class adds the API-layer concerns:
/// whether Kafka is enabled at all, and how long to wait for a render result before
/// returning a 504 to the caller.
/// </para>
/// </summary>
public sealed class ApiKafkaOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Kafka";

    /// <summary>
    /// When <see langword="true"/> the API publishes render requests to <c>render.requests</c>
    /// and awaits results from <c>render.results</c> via Kafka, offloading all Chromium work
    /// to the Console render service.
    ///
    /// When <see langword="false"/> (the default) the API renders inline using its own
    /// embedded Chromium pool — no Kafka dependency required.
    /// </summary>
    public bool Enabled { get; init; } = false;

    /// <summary>
    /// How many seconds the API waits for a <c>render.results</c> reply before returning
    /// HTTP 504 to the Bridge. Defaults to 25 seconds, leaving headroom within the Bridge's
    /// 30-second client timeout.
    /// </summary>
    public int ResultTimeoutSeconds { get; init; } = 25;

    /// <summary>Comma-separated Kafka bootstrap brokers. Defaults to <c>localhost:9092</c>.</summary>
    public string BootstrapServers { get; init; } = "localhost:9092";

    /// <summary>Topic to publish render requests to. Defaults to <c>render.requests</c>.</summary>
    public string RequestTopic { get; init; } = "render.requests";

    /// <summary>Topic to consume render results from. Defaults to <c>render.results</c>.</summary>
    public string ResultTopic { get; init; } = "render.results";
}

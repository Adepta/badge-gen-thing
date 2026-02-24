namespace DocumentGenerator.Core.Interfaces;

/// <summary>
/// Abstraction over a message queue consumer.
/// Implemented by <c>KafkaConsumerService</c> in DocumentGenerator.Messaging.
/// Could equally be implemented for Azure Service Bus, SQS, etc.
/// </summary>
public interface IQueueConsumer
{
    /// <summary>Starts consuming messages from the configured topic/queue.</summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>Gracefully drains in-flight work and stops the consumer.</summary>
    Task StopAsync(CancellationToken cancellationToken = default);
}

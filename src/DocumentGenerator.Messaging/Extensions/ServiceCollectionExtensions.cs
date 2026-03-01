using DocumentGenerator.Messaging.Configuration;
using DocumentGenerator.Messaging.Handlers;
using DocumentGenerator.Messaging.Messages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Rebus.Bus;
using Rebus.Config;
using Rebus.Kafka;
using Rebus.Retry.Simple;
using Rebus.Routing.TypeBased;
using Rebus.ServiceProvider;

namespace DocumentGenerator.Messaging.Extensions;

/// <summary>
/// Extension methods for registering <c>DocumentGenerator.Messaging</c> services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers Rebus with the Kafka transport.
    ///
    /// Rebus acts as both consumer and producer:
    ///   - Subscribes to <c>render.requests</c> via the input queue
    ///   - Publishes <see cref="DocumentRenderResult"/> to <c>render.results</c>
    ///
    /// Worker thread count is intentionally kept at or below
    /// <see cref="KafkaOptions.MaxConcurrentRenders"/> to match the Chromium pool size.
    /// </summary>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="options">Resolved Kafka connection and topic configuration.</param>
    /// <returns>The same <paramref name="services"/> for chaining.</returns>
    public static IServiceCollection AddRebusKafkaMessaging(
        this IServiceCollection services,
        KafkaOptions options)
    {
        // Register the message handler via Rebus's own registration so it can be resolved
        services.AddRebusHandler<DocumentRenderRequestHandler>();

        services.AddRebus(
            (configure, provider) => configure
                .Transport(t => t.UseKafka(
                    options.BootstrapServers,
                    options.RequestTopic))           // input queue = render.requests topic
                .Routing(r => r.TypeBased()
                    .Map<DocumentRenderResult>(options.ResultTopic))  // outbound route
                // Retry transient failures (BrowserPoolException) with exponential backoff.
                // After MaxRetries the message is moved to the dead-letter queue (error queue).
                .Options(o => o.RetryStrategy(
                    errorQueueName:            options.DeadLetterTopic,
                    maxDeliveryAttempts:       options.MaxRetries + 1, // +1 because first attempt counts
                    secondLevelRetriesEnabled: false))
                .Options(o =>
                {
                    o.SetMaxParallelism(options.MaxConcurrentRenders);
                    o.SetNumberOfWorkers(options.MaxConcurrentRenders);
                }),
            onCreated: _ => Task.CompletedTask
        );

        // Subscribe after the host is fully started to avoid deadlocking onCreated
        services.AddHostedService<RebusSubscriptionService>();

        return services;
    }
}

/// <summary>
/// Calls bus.Subscribe after the host has fully started, avoiding the
/// deadlock that occurs when Subscribe is called inside Rebus's onCreated callback.
/// </summary>
internal sealed class RebusSubscriptionService(IBus bus, ILogger<RebusSubscriptionService> logger) : IHostedService
{
    /// <inheritdoc/>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Subscribing to Kafka topic for {MessageType}", nameof(DocumentRenderRequest));

        await bus.Subscribe<DocumentRenderRequest>();

        logger.LogInformation(
            "Rebus subscription active — listening for {MessageType}", nameof(DocumentRenderRequest));
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Rebus subscription stopping.");
        return Task.CompletedTask;
    }
}

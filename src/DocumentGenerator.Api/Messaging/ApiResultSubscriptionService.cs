using DocumentGenerator.Messaging.Messages;
using Rebus.Bus;

namespace DocumentGenerator.Api.Messaging;

/// <summary>
/// Subscribes to <see cref="DocumentRenderResult"/> on <c>render.results</c> after the host
/// has fully started. Calling <c>bus.Subscribe</c> inside the Rebus <c>onCreated</c> callback
/// deadlocks because Rebus hasn't finished initialising its own hosted service at that point.
/// </summary>
internal sealed class ApiResultSubscriptionService(
    IBus bus,
    ILogger<ApiResultSubscriptionService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Subscribing to render.results topic for {MessageType}",
            nameof(DocumentRenderResult));

        await bus.Subscribe<DocumentRenderResult>();

        logger.LogInformation("Kafka consumer group subscribed — waiting for render results");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

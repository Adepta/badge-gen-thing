using DocumentGenerator.Messaging.Messages;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Rebus.Bus;

namespace DocumentGenerator.TestProducer.Messaging;

/// <summary>
/// Subscribes to <see cref="DocumentRenderResult"/> on <c>render.results</c> after the
/// host has fully started. Calling <c>bus.Subscribe</c> inside the Rebus <c>onCreated</c>
/// callback deadlocks because Rebus hasn't finished initialising its hosted service yet.
/// </summary>
internal sealed class ProducerResultSubscriptionService(
    IBus bus,
    ILogger<ProducerResultSubscriptionService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Subscribing to render.results for {MessageType}",
            nameof(DocumentRenderResult));

        await bus.Subscribe<DocumentRenderResult>();

        logger.LogInformation("Producer subscribed — waiting for render results");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

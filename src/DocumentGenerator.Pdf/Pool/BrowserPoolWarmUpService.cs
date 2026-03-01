using DocumentGenerator.Core.Configuration;
using DocumentGenerator.Core.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PuppeteerSharp;

namespace DocumentGenerator.Pdf.Pool;

/// <summary>
/// Background service that pre-warms the browser pool at startup by acquiring and
/// immediately releasing <see cref="BrowserPoolOptions.MinSize"/> leases.
///
/// This ensures the first <c>MinSize</c> real render requests are served by already-
/// running Chromium instances rather than paying the cold-start penalty on the first
/// user-visible requests.
/// </summary>
public sealed class BrowserPoolWarmUpService : IHostedService
{
    private readonly IBrowserPool<IBrowser> _pool;
    private readonly BrowserPoolOptions    _options;
    private readonly ILogger<BrowserPoolWarmUpService> _logger;

    public BrowserPoolWarmUpService(
        IBrowserPool<IBrowser>              pool,
        IOptions<BrowserPoolOptions>        options,
        ILogger<BrowserPoolWarmUpService>   logger)
    {
        _pool    = pool;
        _options = options.Value;
        _logger  = logger;
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var target = _options.MinSize;
        if (target <= 0) return;

        _logger.LogInformation(
            "Pre-warming browser pool: launching {MinSize} Chromium instance(s)", target);

        var leases = new List<IBrowserLease<IBrowser>>(target);
        try
        {
            for (var i = 0; i < target; i++)
            {
                // Each Acquire waits up to AcquireTimeout; use a short combined budget.
                leases.Add(await _pool.AcquireAsync(cancellationToken));
            }

            _logger.LogInformation(
                "Browser pool pre-warm complete — {Count} instance(s) ready", leases.Count);
        }
        catch (Exception ex)
        {
            // Warm-up failure is non-fatal — the pool will launch browsers on demand.
            _logger.LogWarning(ex,
                "Browser pool pre-warm failed after {Count}/{Target} instance(s) — " +
                "browsers will be launched on first request",
                leases.Count, target);
        }
        finally
        {
            // Return all leases so browsers go back to the idle queue.
            foreach (var lease in leases)
                await lease.DisposeAsync();
        }
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

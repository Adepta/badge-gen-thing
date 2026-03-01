using DocumentGenerator.Core.Interfaces;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DocumentGenerator.Api.HealthChecks;

/// <summary>
/// Verifies that the Chromium browser pool can acquire and immediately release a lease.
/// This confirms Chromium is installed, can launch, and the pool is not exhausted or disposed.
/// </summary>
/// <remarks>
/// The check is skipped (returns Healthy with a note) when Kafka is enabled, because in that
/// mode the Console owns the pool — the API does not use it and it may be at min-size (0 warm
/// browsers). A degraded pool in Kafka mode does not indicate a problem with the API itself.
/// </remarks>
public sealed class ChromiumPoolHealthCheck : IHealthCheck
{
    private readonly IBrowserPool<PuppeteerSharp.IBrowser> _pool;
    private readonly IConfiguration _configuration;

    /// <summary>Initialises the check.</summary>
    public ChromiumPoolHealthCheck(
        IBrowserPool<PuppeteerSharp.IBrowser> pool,
        IConfiguration configuration)
    {
        _pool          = pool;
        _configuration = configuration;
    }

    /// <inheritdoc/>
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken  cancellationToken = default)
    {
        // In Kafka mode the API does not own the Chromium pool.
        var kafkaEnabled = _configuration.GetValue<bool>("Kafka:Enabled");
        if (kafkaEnabled)
            return HealthCheckResult.Healthy("Chromium pool check skipped — Kafka mode active.");

        try
        {
            await using var lease = await _pool.AcquireAsync(cancellationToken);
            return HealthCheckResult.Healthy("Chromium pool is healthy.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                "Chromium pool is unhealthy — could not acquire a browser lease.",
                ex);
        }
    }
}

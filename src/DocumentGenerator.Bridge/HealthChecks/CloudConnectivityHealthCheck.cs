using DocumentGenerator.Bridge.Configuration;
using DocumentGenerator.Bridge.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace DocumentGenerator.Bridge.HealthChecks;

/// <summary>
/// Probes the cloud API's <c>/health</c> endpoint to verify the bridge can reach it.
/// Returns <see cref="HealthStatus.Degraded"/> rather than <see cref="HealthStatus.Unhealthy"/>
/// because the bridge can still serve cached/queued requests even if the cloud is temporarily down.
/// </summary>
public sealed class CloudConnectivityHealthCheck(
    IHttpClientFactory httpClientFactory,
    IOptionsMonitor<CloudOptions> cloudOptions) : IHealthCheck
{
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly IOptionsMonitor<CloudOptions> _cloudOptions = cloudOptions;

    /// <inheritdoc/>
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken  cancellationToken = default)
    {
        var opts = _cloudOptions.CurrentValue;

        if (string.IsNullOrWhiteSpace(opts.BaseUrl))
            return HealthCheckResult.Degraded("Cloud:BaseUrl is not configured — setup wizard not completed.");

        try
        {
            using var client = _httpClientFactory.CreateClient(CloudBadgeClient.HttpClientName);
            using var cts    = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(5));

            var response = await client.GetAsync("/health", cts.Token);

            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy($"Cloud API reachable — {opts.BaseUrl}")
                : HealthCheckResult.Degraded(
                    $"Cloud API returned HTTP {(int)response.StatusCode} — {opts.BaseUrl}");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Degraded(
                $"Cloud API unreachable — {opts.BaseUrl}: {ex.Message}",
                ex);
        }
    }
}

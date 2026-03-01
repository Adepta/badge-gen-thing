using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DocumentGenerator.Api.HealthChecks;

/// <summary>
/// Verifies that the templates directory exists and contains at least one <c>.html</c> file.
/// A missing or empty templates directory means the API cannot serve any render requests.
/// </summary>
public sealed class TemplateDirHealthCheck : IHealthCheck
{
    private readonly IConfiguration _configuration;

    /// <summary>Initialises the check with application configuration.</summary>
    public TemplateDirHealthCheck(IConfiguration configuration)
        => _configuration = configuration;

    /// <inheritdoc/>
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken  cancellationToken = default)
    {
        var configured = _configuration["DocumentGenerator:TemplatesPath"] ?? "templates";
        var path = Path.IsPathRooted(configured)
            ? configured
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, configured));

        if (!Directory.Exists(path))
            return Task.FromResult(HealthCheckResult.Unhealthy(
                $"Templates directory does not exist: {path}"));

        var count = Directory.EnumerateFiles(path, "*.html").Count();
        if (count == 0)
            return Task.FromResult(HealthCheckResult.Degraded(
                $"Templates directory is empty (no .html files): {path}"));

        return Task.FromResult(HealthCheckResult.Healthy(
            $"Templates directory OK — {count} template(s) found.",
            new Dictionary<string, object> { ["path"] = path, ["count"] = count }));
    }
}

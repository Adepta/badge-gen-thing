using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DocumentGenerator.Api.HealthChecks;

/// <summary>
/// Writes health check results as a JSON object compatible with standard readiness probes.
/// </summary>
public static class HealthCheckResponseWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented       = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Serialises the <paramref name="report"/> to JSON and writes it to the HTTP response.
    /// </summary>
    public static async Task WriteJsonAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode  = report.Status == HealthStatus.Healthy
            ? StatusCodes.Status200OK
            : StatusCodes.Status503ServiceUnavailable;

        var result = new
        {
            status  = report.Status.ToString(),
            utc     = DateTimeOffset.UtcNow,
            checks  = report.Entries.Select(e => new
            {
                name        = e.Key,
                status      = e.Value.Status.ToString(),
                description = e.Value.Description,
                duration    = e.Value.Duration,
                data        = e.Value.Data.Count > 0 ? e.Value.Data : null,
                error       = e.Value.Exception?.Message
            })
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(result, JsonOptions));
    }
}

using System.ComponentModel.DataAnnotations;

namespace DocumentGenerator.Api.Configuration;

/// <summary>
/// Sliding-window rate-limit applied to <c>POST /api/badges/render</c>.
/// Each client (identified by IP address) is limited to <see cref="PermitLimit"/>
/// render requests per <see cref="Window"/>.
/// </summary>
public sealed class RateLimitOptions
{
    public const string SectionName = "RateLimit";

    /// <summary>
    /// Maximum number of render requests permitted per <see cref="Window"/>.
    /// Defaults to 10.
    /// </summary>
    [Range(1, int.MaxValue, ErrorMessage = "RateLimit:PermitLimit must be at least 1.")]
    public int PermitLimit { get; init; } = 10;

    /// <summary>
    /// Length of the sliding window. Defaults to 60 seconds.
    /// </summary>
    [Range(typeof(TimeSpan), "00:00:01", "1.00:00:00",
        ErrorMessage = "RateLimit:Window must be between 1 second and 24 hours.")]
    public TimeSpan Window { get; init; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Number of segments the window is divided into for the sliding-window algorithm.
    /// Higher values give smoother throttling. Defaults to 4.
    /// </summary>
    [Range(1, 100, ErrorMessage = "RateLimit:SegmentsPerWindow must be between 1 and 100.")]
    public int SegmentsPerWindow { get; init; } = 4;

    /// <summary>
    /// HTTP status code returned when the limit is exceeded.
    /// Defaults to 429 (Too Many Requests).
    /// </summary>
    public int RejectionStatusCode { get; init; } = StatusCodes.Status429TooManyRequests;
}

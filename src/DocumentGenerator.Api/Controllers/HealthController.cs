using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DocumentGenerator.Api.Controllers;

/// <summary>
/// Lightweight health-check endpoint — no authentication required so load balancers
/// and the bridge service can probe liveness without an API key.
/// </summary>
[ApiController]
[Route("health")]
[AllowAnonymous]
public sealed class HealthController : ControllerBase
{
    /// <summary>
    /// Returns a simple 200 OK response confirming the API is running.
    /// </summary>
    /// <returns>200 OK with a <c>{ status: "healthy" }</c> payload.</returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Get() =>
        Ok(new { status = "healthy", utc = DateTimeOffset.UtcNow });
}

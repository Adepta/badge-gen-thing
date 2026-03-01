using DocumentGenerator.Bridge.Configuration;
using Microsoft.Extensions.Options;

namespace DocumentGenerator.Bridge.Middleware;

/// <summary>
/// Middleware that redirects all non-setup requests to the setup wizard
/// when the bridge has not yet been configured.
/// </summary>
/// <remarks>
/// Once <c>Bridge:IsConfigured</c> is <c>true</c> in <c>appsettings.json</c>,
/// this middleware passes all requests through transparently.
/// </remarks>
public sealed class SetupGuardMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SetupGuardMiddleware> _logger;

    /// <summary>
    /// Initialises a new <see cref="SetupGuardMiddleware"/>.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    public SetupGuardMiddleware(RequestDelegate next, ILogger<SetupGuardMiddleware> logger)
    {
        _next   = next;
        _logger = logger;
    }

    /// <summary>
    /// Intercepts requests and redirects to <c>/setup</c> when the bridge is unconfigured.
    /// Setup-related paths (<c>/setup</c>, <c>/health</c>) are always allowed through.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    /// <param name="options">Live bridge options — checked on every request.</param>
    public async Task InvokeAsync(HttpContext context, IOptionsMonitor<BridgeOptions> options)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        // Always allow: health probe, setup wizard, setup API calls, static assets
        var isAllowed = path.StartsWith("/setup", StringComparison.OrdinalIgnoreCase)
                     || path.Equals("/health", StringComparison.OrdinalIgnoreCase)
                     || path.StartsWith("/wwwroot", StringComparison.OrdinalIgnoreCase);

        if (!options.CurrentValue.IsConfigured && !isAllowed)
        {
            _logger.LogInformation(
                "Bridge not configured — redirecting {Path} to /setup", path);
            context.Response.Redirect("/setup");
            return;
        }

        await _next(context);
    }
}

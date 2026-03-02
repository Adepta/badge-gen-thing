using DocumentGenerator.Bridge.Configuration;
using Microsoft.Extensions.Options;

namespace DocumentGenerator.Bridge.Middleware;

/// <summary>
/// Simple shared-token authentication for Bridge endpoints.
///
/// When <c>Bridge:AccessToken</c> is configured, every request to non-setup and
/// non-health endpoints must include the header <c>X-Bridge-Token: {token}</c>.
///
/// This provides a basic layer of protection against unauthorised LAN hosts
/// triggering print jobs. It is not a substitute for TLS or mTLS but is a
/// pragmatic improvement over completely open endpoints.
///
/// If <c>Bridge:AccessToken</c> is empty or absent, authentication is skipped
/// (backward-compatible default — setup wizard supplies the token during
/// first-run configuration).
/// </summary>
public sealed class BridgeTokenAuthMiddleware
{
    private readonly RequestDelegate _next;

    /// <summary>Name of the HTTP request header carrying the access token.</summary>
    public const string TokenHeader = "X-Bridge-Token";

    public BridgeTokenAuthMiddleware(RequestDelegate next) => _next = next;

    /// <inheritdoc cref="IMiddleware.InvokeAsync"/>
    public async Task InvokeAsync(
        HttpContext context,
        IOptionsMonitor<BridgeOptions> opts)
    {
        var token = opts.CurrentValue.AccessToken;

        // Auth is disabled when no token is configured.
        if (string.IsNullOrWhiteSpace(token))
        {
            await _next(context);
            return;
        }

        // Health and setup endpoints are always public.
        var path = context.Request.Path.Value ?? string.Empty;
        if (path.StartsWith("/health", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/setup",  StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        // Validate the token with a timing-safe comparison.
        if (!context.Request.Headers.TryGetValue(TokenHeader, out var provided) ||
            !System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(token),
                System.Text.Encoding.UTF8.GetBytes(provided.ToString())))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new
            {
                error = $"Missing or invalid {TokenHeader} header."
            });
            return;
        }

        await _next(context);
    }
}

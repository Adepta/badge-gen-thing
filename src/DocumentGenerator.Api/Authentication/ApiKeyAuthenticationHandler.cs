using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using DocumentGenerator.Core.Errors;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace DocumentGenerator.Api.Authentication;

/// <summary>
/// ASP.NET Core authentication handler that validates requests using a shared API key
/// supplied in the <c>X-Api-Key</c> HTTP header.
/// </summary>
/// <remarks>
/// The expected key is read from <c>ApiAuth:ApiKey</c> in application configuration.
/// Requests missing or presenting an invalid key receive a 401 Unauthorized response.
/// </remarks>
public sealed class ApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    /// <summary>The HTTP header name carrying the API key.</summary>
    public const string HeaderName = "X-Api-Key";

    /// <summary>The authentication scheme name registered in DI.</summary>
    public const string SchemeName = "ApiKey";

    private readonly string _expectedKey;

    /// <inheritdoc />
    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IConfiguration configuration)
        : base(options, logger, encoder)
    {
        _expectedKey = configuration["ApiAuth:ApiKey"]
            ?? throw ConfigurationException.Missing("ApiAuth:ApiKey");
    }

    /// <summary>
    /// Validates the <c>X-Api-Key</c> header against the configured key.
    /// Returns <see cref="AuthenticateResult.Success"/> on a match,
    /// <see cref="AuthenticateResult.NoResult"/> for anonymous endpoints (e.g. <c>/health</c>),
    /// or <c>AuthenticateResult.Fail</c> otherwise.
    /// </summary>
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Skip auth silently for anonymous endpoints — avoids noisy log entries
        // for health probes that legitimately carry no key.
        if (Request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(AuthenticateResult.NoResult());

        if (!Request.Headers.TryGetValue(HeaderName, out var providedKey))
            return Task.FromResult(AuthenticateResult.Fail($"Missing {HeaderName} header."));

        // Constant-time comparison — prevents timing oracle attacks on the key.
        var providedBytes = Encoding.UTF8.GetBytes(providedKey.ToString());
        var expectedBytes = Encoding.UTF8.GetBytes(_expectedKey);
        if (!CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes))
            return Task.FromResult(AuthenticateResult.Fail("Invalid API key."));

        var claims = new[] { new Claim(ClaimTypes.Name, "bridge-client") };
        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

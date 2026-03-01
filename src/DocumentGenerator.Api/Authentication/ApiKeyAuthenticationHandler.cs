using System.Security.Claims;
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
    /// or <c>AuthenticateResult.Fail</c> otherwise.
    /// </summary>
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var providedKey))
            return Task.FromResult(AuthenticateResult.Fail($"Missing {HeaderName} header."));

        if (!string.Equals(providedKey, _expectedKey, StringComparison.Ordinal))
            return Task.FromResult(AuthenticateResult.Fail("Invalid API key."));

        var claims = new[] { new Claim(ClaimTypes.Name, "bridge-client") };
        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

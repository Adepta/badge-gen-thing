using DocumentGenerator.Bridge.Configuration;
using DocumentGenerator.Bridge.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace DocumentGenerator.UnitTests.Bridge;

/// <summary>
/// Unit tests for <see cref="BridgeTokenAuthMiddleware"/>.
/// Uses <see cref="DefaultHttpContext"/> — no web host required.
/// </summary>
public sealed class BridgeTokenAuthMiddlewareTests
{
    private const string ValidToken = "super-secret-token";

    // ── No token configured — middleware is transparent ───────────────────────

    [Fact]
    public async Task InvokeAsync_NoTokenConfigured_PassesThrough()
    {
        var nextCalled = false;
        var ctx        = MakeContext("GET", "/api/render");
        var sut        = BuildMiddleware(configuredToken: null, _ => { nextCalled = true; return Task.CompletedTask; });

        await sut.InvokeAsync(ctx, Options(configuredToken: null));

        nextCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task InvokeAsync_EmptyTokenConfigured_PassesThrough()
    {
        var nextCalled = false;
        var ctx        = MakeContext("GET", "/api/render");
        var sut        = BuildMiddleware(configuredToken: "", _ => { nextCalled = true; return Task.CompletedTask; });

        await sut.InvokeAsync(ctx, Options(configuredToken: ""));

        nextCalled.ShouldBeTrue();
    }

    // ── Health / setup bypass — even when token is configured ─────────────────

    [Fact]
    public async Task InvokeAsync_HealthPath_PassesThroughWithoutToken()
    {
        var nextCalled = false;
        var ctx        = MakeContext("GET", "/health");
        var sut        = BuildMiddleware(configuredToken: ValidToken, _ => { nextCalled = true; return Task.CompletedTask; });

        await sut.InvokeAsync(ctx, Options(configuredToken: ValidToken));

        nextCalled.ShouldBeTrue();
        ctx.Response.StatusCode.ShouldNotBe(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task InvokeAsync_SetupPath_PassesThroughWithoutToken()
    {
        var nextCalled = false;
        var ctx        = MakeContext("GET", "/setup/complete");
        var sut        = BuildMiddleware(configuredToken: ValidToken, _ => { nextCalled = true; return Task.CompletedTask; });

        await sut.InvokeAsync(ctx, Options(configuredToken: ValidToken));

        nextCalled.ShouldBeTrue();
    }

    // ── Token present and correct ─────────────────────────────────────────────

    [Fact]
    public async Task InvokeAsync_CorrectToken_PassesThrough()
    {
        var nextCalled = false;
        var ctx        = MakeContext("POST", "/api/render", token: ValidToken);
        var sut        = BuildMiddleware(configuredToken: ValidToken, _ => { nextCalled = true; return Task.CompletedTask; });

        await sut.InvokeAsync(ctx, Options(configuredToken: ValidToken));

        nextCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task InvokeAsync_CorrectToken_Returns200Range()
    {
        var ctx = MakeContext("POST", "/api/render", token: ValidToken);
        var sut = BuildMiddleware(configuredToken: ValidToken, _ => Task.CompletedTask);

        await sut.InvokeAsync(ctx, Options(configuredToken: ValidToken));

        ctx.Response.StatusCode.ShouldNotBe(StatusCodes.Status401Unauthorized);
    }

    // ── Token missing ─────────────────────────────────────────────────────────

    [Fact]
    public async Task InvokeAsync_TokenConfigured_NoHeaderProvided_Returns401()
    {
        var ctx = MakeContext("POST", "/api/render", token: null);
        var sut = BuildMiddleware(configuredToken: ValidToken, _ => Task.CompletedTask);

        await sut.InvokeAsync(ctx, Options(configuredToken: ValidToken));

        ctx.Response.StatusCode.ShouldBe(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task InvokeAsync_TokenConfigured_NoHeaderProvided_DoesNotCallNext()
    {
        var nextCalled = false;
        var ctx        = MakeContext("POST", "/api/render", token: null);
        var sut        = BuildMiddleware(configuredToken: ValidToken, _ => { nextCalled = true; return Task.CompletedTask; });

        await sut.InvokeAsync(ctx, Options(configuredToken: ValidToken));

        nextCalled.ShouldBeFalse();
    }

    // ── Token wrong ───────────────────────────────────────────────────────────

    [Fact]
    public async Task InvokeAsync_WrongToken_Returns401()
    {
        var ctx = MakeContext("POST", "/api/render", token: "wrong-token");
        var sut = BuildMiddleware(configuredToken: ValidToken, _ => Task.CompletedTask);

        await sut.InvokeAsync(ctx, Options(configuredToken: ValidToken));

        ctx.Response.StatusCode.ShouldBe(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task InvokeAsync_WrongToken_DoesNotCallNext()
    {
        var nextCalled = false;
        var ctx        = MakeContext("POST", "/api/render", token: "bad");
        var sut        = BuildMiddleware(configuredToken: ValidToken, _ => { nextCalled = true; return Task.CompletedTask; });

        await sut.InvokeAsync(ctx, Options(configuredToken: ValidToken));

        nextCalled.ShouldBeFalse();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static BridgeTokenAuthMiddleware BuildMiddleware(string? configuredToken, RequestDelegate next)
        => new(next);

    private static DefaultHttpContext MakeContext(string method, string path, string? token = null)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Method = method;
        ctx.Request.Path   = path;
        ctx.Response.Body  = new MemoryStream(); // needed for WriteAsJsonAsync
        if (token is not null)
            ctx.Request.Headers[BridgeTokenAuthMiddleware.TokenHeader] = token;
        return ctx;
    }

    private static IOptionsMonitor<BridgeOptions> Options(string? configuredToken)
    {
        var opts = new BridgeOptions { AccessToken = configuredToken ?? string.Empty };
        var mock = new Moq.Mock<IOptionsMonitor<BridgeOptions>>();
        mock.Setup(m => m.CurrentValue).Returns(opts);
        return mock.Object;
    }
}

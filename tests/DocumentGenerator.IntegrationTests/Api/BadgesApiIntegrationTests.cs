using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DocumentGenerator.Api.Models;
using DocumentGenerator.Core.Interfaces;
using DocumentGenerator.Core.Models;
using Moq;
using Shouldly;
using Xunit;

namespace DocumentGenerator.IntegrationTests.Api;

/// <summary>
/// Integration tests for <c>DocumentGenerator.Api</c> endpoints using
/// <see cref="ApiWebApplicationFactory"/> (in-process test server, no Chromium).
/// </summary>
public sealed class BadgesApiIntegrationTests : IClassFixture<ApiWebApplicationFactory>
{
    private readonly ApiWebApplicationFactory _factory;
    private readonly HttpClient               _client;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public BadgesApiIntegrationTests(ApiWebApplicationFactory factory)
    {
        _factory = factory;
        _client  = factory.CreateClient();
        _client.DefaultRequestHeaders.Add("X-Api-Key", ApiWebApplicationFactory.TestApiKey);
    }

    // ── /health ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Health_Get_Returns200()
    {
        var response = await _client.GetAsync("/health");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Health_Get_ReturnsHealthyStatus()
    {
        var response = await _client.GetAsync("/health");
        var body     = await response.Content.ReadAsStringAsync();
        body.ShouldContain("healthy");
    }

    [Fact]
    public async Task Health_Get_NoApiKeyRequired()
    {
        using var anonClient = _factory.CreateClient();
        var response         = await anonClient.GetAsync("/health");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // ── /api/badges/templates ────────────────────────────────────────────────

    [Fact]
    public async Task Templates_Get_Returns200()
    {
        var response = await _client.GetAsync("/api/badges/templates");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Templates_Get_ReturnsTemplateNames()
    {
        var response  = await _client.GetAsync("/api/badges/templates");
        var templates = await response.Content.ReadFromJsonAsync<IEnumerable<string>>(JsonOpts);
        templates!.ShouldContain("badge-pulse-a6");
    }

    [Fact]
    public async Task Templates_Get_MissingApiKey_Returns401()
    {
        using var anonClient = _factory.CreateClient();
        var response         = await anonClient.GetAsync("/api/badges/templates");
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Templates_Get_WrongApiKey_Returns401()
    {
        using var badClient = _factory.CreateClient();
        badClient.DefaultRequestHeaders.Add("X-Api-Key", "wrong-key");
        var response = await badClient.GetAsync("/api/badges/templates");
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ── POST /api/badges/render — success ─────────────────────────────────────

    [Fact]
    public async Task Render_ValidRequest_Returns200()
    {
        var response = await _client.PostAsJsonAsync("/api/badges/render", BuildRenderRequest());
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Render_ValidRequest_ResponseSuccessIsTrue()
    {
        var response = await _client.PostAsJsonAsync("/api/badges/render", BuildRenderRequest());
        var body     = await response.Content.ReadFromJsonAsync<BadgeRenderResponse>(JsonOpts);
        body!.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task Render_ValidRequest_DocumentBase64IsPopulated()
    {
        var response = await _client.PostAsJsonAsync("/api/badges/render", BuildRenderRequest());
        var body     = await response.Content.ReadFromJsonAsync<BadgeRenderResponse>(JsonOpts);
        body!.DocumentBase64.ShouldBe(Convert.ToBase64String(ApiWebApplicationFactory.FakePdfBytes));
    }

    [Fact]
    public async Task Render_ValidRequest_EchoesCorrelationId()
    {
        var correlationId = Guid.NewGuid();
        var response      = await _client.PostAsJsonAsync("/api/badges/render", BuildRenderRequest(correlationId));
        var body          = await response.Content.ReadFromJsonAsync<BadgeRenderResponse>(JsonOpts);
        body!.CorrelationId.ShouldBe(correlationId);
    }

    [Fact]
    public async Task Render_ValidRequest_MimeTypeIsPdf()
    {
        var response = await _client.PostAsJsonAsync("/api/badges/render", BuildRenderRequest());
        var body     = await response.Content.ReadFromJsonAsync<BadgeRenderResponse>(JsonOpts);
        body!.MimeType.ShouldBe("application/pdf");
    }

    [Fact]
    public async Task Render_PngFormat_MimeTypeIsPng()
    {
        var response = await _client.PostAsJsonAsync("/api/badges/render", BuildRenderRequest(format: "Png"));
        var body     = await response.Content.ReadFromJsonAsync<BadgeRenderResponse>(JsonOpts);
        body!.MimeType.ShouldBe("image/png");
    }

    // ── POST /api/badges/render — auth ────────────────────────────────────────

    [Fact]
    public async Task Render_MissingApiKey_Returns401()
    {
        using var anonClient = _factory.CreateClient();
        var response         = await anonClient.PostAsJsonAsync("/api/badges/render", BuildRenderRequest());
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Render_WrongApiKey_Returns401()
    {
        using var badClient = _factory.CreateClient();
        badClient.DefaultRequestHeaders.Add("X-Api-Key", "not-the-key");
        var response = await badClient.PostAsJsonAsync("/api/badges/render", BuildRenderRequest());
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ── POST /api/badges/render — validation ──────────────────────────────────

    [Fact]
    public async Task Render_UnknownTemplate_Returns400()
    {
        var request  = BuildRenderRequest(templateName: "does-not-exist");
        var response = await _client.PostAsJsonAsync("/api/badges/render", request);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Render_UnknownTemplate_ResponseSuccessIsFalse()
    {
        var request  = BuildRenderRequest(templateName: "does-not-exist");
        var response = await _client.PostAsJsonAsync("/api/badges/render", request);
        var body     = await response.Content.ReadFromJsonAsync<BadgeRenderResponse>(JsonOpts);
        body!.Success.ShouldBeFalse();
        body.Error.ShouldNotBeNullOrEmpty();
    }

    // ── POST /api/badges/render — pipeline failure ────────────────────────────

    [Fact]
    public async Task Render_PipelineThrows_Returns500()
    {
        _factory.PipelineMock
            .Setup(p => p.ExecuteAsync(It.IsAny<RenderRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("renderer exploded"));

        var response = await _client.PostAsJsonAsync("/api/badges/render", BuildRenderRequest());
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);

        // Restore default behaviour for subsequent tests
        _factory.PipelineMock
            .Setup(p => p.ExecuteAsync(It.IsAny<RenderRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RenderResult.Success(Guid.NewGuid(), ApiWebApplicationFactory.FakePdfBytes,
                TimeSpan.FromMilliseconds(50), "badge"));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static object BuildRenderRequest(
        Guid?   correlationId = null,
        string  templateName  = "badge-pulse-a6",
        string  format        = "Pdf") =>
        new
        {
            templateName,
            variables     = new Dictionary<string, string> { ["firstName"] = "Jane", ["lastName"] = "Smith" },
            format,
            correlationId = correlationId ?? Guid.NewGuid()
        };
}

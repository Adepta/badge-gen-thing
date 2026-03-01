using DocumentGenerator.Api.Controllers;
using DocumentGenerator.Api.Models;
using DocumentGenerator.Api.Services;
using DocumentGenerator.Core.Interfaces;
using DocumentGenerator.Core.Models;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace DocumentGenerator.UnitTests.Api;

/// <summary>
/// Unit tests for <see cref="BadgesController"/>.
/// Both <see cref="IDocumentPipeline"/> and the file system (via a temp dir) are
/// controlled so no Chromium or real templates are needed.
/// </summary>
public sealed class BadgesControllerTests : IDisposable
{
    private readonly Mock<IDocumentPipeline> _pipelineMock = new();
    private readonly string                  _tempDir;
    private readonly TemplateLocator         _locator;
    private readonly BadgesController        _sut;

    private static readonly byte[] FakePdfBytes = [0x25, 0x50, 0x44, 0x46]; // %PDF

    public BadgesControllerTests()
    {
        // Temp dir with a single stub template file
        _tempDir = Path.Combine(Path.GetTempPath(), $"ctrl_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        File.WriteAllText(Path.Combine(_tempDir, "badge-pulse-a6.html"), "<p>{{variables.firstName}}</p>");
        File.WriteAllText(Path.Combine(_tempDir, "badge-pulse-a6.css"),  "body{}");

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DocumentGenerator:TemplatesPath"] = _tempDir
            })
            .Build();

        _locator = new TemplateLocator(config, NullLogger<TemplateLocator>.Instance);

        _sut = new BadgesController(
            _pipelineMock.Object,
            _locator,
            NullLogger<BadgesController>.Instance);
    }

    // ── Happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task RenderAsync_ValidRequest_Returns200()
    {
        ArrangePipelineSuccess();
        var request = BuildRequest();

        var result = await _sut.RenderAsync(request, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>()
            .Which.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task RenderAsync_ValidRequest_ResponseSuccessIsTrue()
    {
        ArrangePipelineSuccess();
        var result = (OkObjectResult)await _sut.RenderAsync(BuildRequest(), CancellationToken.None);
        var body   = (BadgeRenderResponse)result.Value!;

        body.Success.Should().BeTrue();
    }

    [Fact]
    public async Task RenderAsync_ValidRequest_DocumentBase64IsPopulated()
    {
        ArrangePipelineSuccess();
        var result = (OkObjectResult)await _sut.RenderAsync(BuildRequest(), CancellationToken.None);
        var body   = (BadgeRenderResponse)result.Value!;

        body.DocumentBase64.Should().Be(Convert.ToBase64String(FakePdfBytes));
    }

    [Fact]
    public async Task RenderAsync_ValidRequest_EchoesCorrelationId()
    {
        ArrangePipelineSuccess();
        var correlationId = Guid.NewGuid();
        var request       = BuildRequest(correlationId: correlationId);

        var result = (OkObjectResult)await _sut.RenderAsync(request, CancellationToken.None);
        var body   = (BadgeRenderResponse)result.Value!;

        body.CorrelationId.Should().Be(correlationId);
    }

    [Fact]
    public async Task RenderAsync_NullCorrelationId_GeneratesOne()
    {
        ArrangePipelineSuccess();
        var request = BuildRequest(correlationId: null);

        var result = (OkObjectResult)await _sut.RenderAsync(request, CancellationToken.None);
        var body   = (BadgeRenderResponse)result.Value!;

        body.CorrelationId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task RenderAsync_PdfFormat_SetsPdfMimeType()
    {
        ArrangePipelineSuccess();
        var result = (OkObjectResult)await _sut.RenderAsync(BuildRequest(format: "Pdf"), CancellationToken.None);
        var body   = (BadgeRenderResponse)result.Value!;

        body.MimeType.Should().Be("application/pdf");
    }

    [Fact]
    public async Task RenderAsync_PngFormat_SetsPngMimeType()
    {
        ArrangePipelineSuccess();
        var result = (OkObjectResult)await _sut.RenderAsync(BuildRequest(format: "Png"), CancellationToken.None);
        var body   = (BadgeRenderResponse)result.Value!;

        body.MimeType.Should().Be("image/png");
    }

    // ── Unknown template ──────────────────────────────────────────────────────

    [Fact]
    public async Task RenderAsync_UnknownTemplate_Returns400()
    {
        var request = BuildRequest(templateName: "does-not-exist");

        var result = await _sut.RenderAsync(request, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>()
            .Which.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task RenderAsync_UnknownTemplate_PipelineIsNotCalled()
    {
        var request = BuildRequest(templateName: "does-not-exist");
        await _sut.RenderAsync(request, CancellationToken.None);

        _pipelineMock.Verify(
            p => p.ExecuteAsync(It.IsAny<RenderRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── Pipeline failure ──────────────────────────────────────────────────────

    [Fact]
    public async Task RenderAsync_PipelineThrows_Returns500()
    {
        _pipelineMock
            .Setup(p => p.ExecuteAsync(It.IsAny<RenderRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("chromium died"));

        var result = await _sut.RenderAsync(BuildRequest(), CancellationToken.None);

        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(500);
    }

    [Fact]
    public async Task RenderAsync_PipelineThrows_ResponseSuccessIsFalse()
    {
        _pipelineMock
            .Setup(p => p.ExecuteAsync(It.IsAny<RenderRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("chromium died"));

        var result = (ObjectResult)await _sut.RenderAsync(BuildRequest(), CancellationToken.None);
        var body   = (BadgeRenderResponse)result.Value!;

        body.Success.Should().BeFalse();
        body.Error.Should().NotBeNullOrEmpty();
    }

    // ── ListTemplates ─────────────────────────────────────────────────────────

    [Fact]
    public void ListTemplates_Returns200WithTemplateNames()
    {
        var result = (OkObjectResult)_sut.ListTemplates();
        var names  = (IEnumerable<string>)result.Value!;

        names.Should().Contain("badge-pulse-a6");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void ArrangePipelineSuccess()
    {
        _pipelineMock
            .Setup(p => p.ExecuteAsync(It.IsAny<RenderRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RenderResult.Success(Guid.NewGuid(), FakePdfBytes, TimeSpan.FromSeconds(1), "badge"));
    }

    private static BadgeRenderRequest BuildRequest(
        string  templateName  = "badge-pulse-a6",
        string  format        = "Pdf",
        Guid?   correlationId = null) =>
        new()
        {
            TemplateName  = templateName,
            Variables     = new Dictionary<string, object?> { ["firstName"] = "Jane" },
            Format        = format,
            CorrelationId = correlationId
        };

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* best-effort */ }
    }
}

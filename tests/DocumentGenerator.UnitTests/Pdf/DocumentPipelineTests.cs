using DocumentGenerator.Core.Interfaces;
using DocumentGenerator.Core.Models;
using DocumentGenerator.Pdf;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace DocumentGenerator.UnitTests.Pdf;

/// <summary>
/// Unit tests for <see cref="DocumentPipeline"/>.
/// The template engine and PDF renderer are mocked so no Chromium process is started.
/// </summary>
public sealed class DocumentPipelineTests
{
    private readonly Mock<ITemplateEngine>    _templateEngineMock = new();
    private readonly Mock<IDocumentRenderer>  _rendererMock       = new();
    private readonly DocumentPipeline         _sut;

    private static readonly byte[] FakePdfBytes = [0x25, 0x50, 0x44, 0x46]; // %PDF

    public DocumentPipelineTests()
    {
        _sut = new DocumentPipeline(
            _templateEngineMock.Object,
            _rendererMock.Object,
            NullLogger<DocumentPipeline>.Instance);
    }

    // -----------------------------------------------------------------------
    // Happy path
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_HappyPath_ReturnsPdfBytes()
    {
        var request = BuildRequest();
        ArrangeSuccess("<html>rendered</html>", FakePdfBytes);

        var result = await _sut.ExecuteAsync(request);

        result.PdfBytes.Should().Equal(FakePdfBytes);
    }

    [Fact]
    public async Task ExecuteAsync_HappyPath_ReturnsMatchingJobId()
    {
        var request = BuildRequest();
        ArrangeSuccess("<html>rendered</html>", FakePdfBytes);

        var result = await _sut.ExecuteAsync(request);

        result.JobId.Should().Be(request.JobId);
    }

    [Fact]
    public async Task ExecuteAsync_HappyPath_ReturnsCorrectDocumentType()
    {
        var request = BuildRequest(documentType: "invoice");
        ArrangeSuccess("<html>invoice</html>", FakePdfBytes);

        var result = await _sut.ExecuteAsync(request);

        result.DocumentType.Should().Be("invoice");
    }

    [Fact]
    public async Task ExecuteAsync_HappyPath_ElapsedTimeIsPositive()
    {
        var request = BuildRequest();
        ArrangeSuccess("<html/>", FakePdfBytes);

        var result = await _sut.ExecuteAsync(request);

        result.ElapsedTime.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task ExecuteAsync_HappyPath_TemplateEngineReceivesCorrectTemplate()
    {
        var request = BuildRequest();
        ArrangeSuccess("<html/>", FakePdfBytes);

        await _sut.ExecuteAsync(request);

        _templateEngineMock.Verify(
            e => e.RenderAsync(request.Template, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_HappyPath_RendererReceivesHtmlFromEngine()
    {
        const string expectedHtml = "<html>rendered by engine</html>";
        var request = BuildRequest();

        _templateEngineMock
            .Setup(e => e.RenderAsync(It.IsAny<DocumentTemplate>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedHtml);

        _rendererMock
            .Setup(r => r.RenderPdfAsync(expectedHtml, It.IsAny<PdfOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(FakePdfBytes);

        await _sut.ExecuteAsync(request);

        _rendererMock.Verify(
            r => r.RenderPdfAsync(expectedHtml, It.IsAny<PdfOptions>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_HappyPath_RendererReceivesCorrectPdfOptions()
    {
        var pdfOptions = new PdfOptions { Format = "Letter", Landscape = true };
        var request    = BuildRequest(pdfOptions: pdfOptions);
        ArrangeSuccess("<html/>", FakePdfBytes);

        await _sut.ExecuteAsync(request);

        _rendererMock.Verify(
            r => r.RenderPdfAsync(
                It.IsAny<string>(),
                It.Is<PdfOptions>(o => o.Format == "Letter" && o.Landscape == true),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // -----------------------------------------------------------------------
    // Error propagation
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_TemplateEngineThrows_ExceptionPropagates()
    {
        var request = BuildRequest();
        _templateEngineMock
            .Setup(e => e.RenderAsync(It.IsAny<DocumentTemplate>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("engine boom"));

        var act = async () => await _sut.ExecuteAsync(request);

        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("engine boom");
    }

    [Fact]
    public async Task ExecuteAsync_RendererThrows_ExceptionPropagates()
    {
        var request = BuildRequest();
        _templateEngineMock
            .Setup(e => e.RenderAsync(It.IsAny<DocumentTemplate>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("<html/>");

        _rendererMock
            .Setup(r => r.RenderPdfAsync(It.IsAny<string>(), It.IsAny<PdfOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("chromium timeout"));

        var act = async () => await _sut.ExecuteAsync(request);

        await act.Should()
            .ThrowAsync<TimeoutException>()
            .WithMessage("chromium timeout");
    }

    [Fact]
    public async Task ExecuteAsync_TemplateEngineThrows_RendererIsNeverCalled()
    {
        var request = BuildRequest();
        _templateEngineMock
            .Setup(e => e.RenderAsync(It.IsAny<DocumentTemplate>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("engine failed"));

        try { await _sut.ExecuteAsync(request); } catch { /* expected */ }

        _rendererMock.Verify(
            r => r.RenderPdfAsync(It.IsAny<string>(), It.IsAny<PdfOptions>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private void ArrangeSuccess(string html, byte[] pdfBytes)
    {
        _templateEngineMock
            .Setup(e => e.RenderAsync(It.IsAny<DocumentTemplate>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(html);

        _rendererMock
            .Setup(r => r.RenderPdfAsync(It.IsAny<string>(), It.IsAny<PdfOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pdfBytes);
    }

    private static RenderRequest BuildRequest(
        string documentType = "test",
        PdfOptions? pdfOptions = null) =>
        new()
        {
            Template = new DocumentTemplate
            {
                DocumentType = documentType,
                Pdf          = pdfOptions ?? new PdfOptions()
            }
        };
}

using DocumentGenerator.Core.Interfaces;
using DocumentGenerator.Core.Models;
using DocumentGenerator.Pdf;
using DocumentGenerator.Templating;
using DocumentGenerator.Templating.Extensions;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace DocumentGenerator.IntegrationTests.Pipeline;

/// <summary>
/// Integration tests that wire the real <see cref="HandlebarsTemplateEngine"/> and
/// <see cref="DocumentPipeline"/> together.  The <see cref="IDocumentRenderer"/> is
/// mocked so no Chromium process is needed, but the full template → HTML path
/// is exercised in-process.
/// </summary>
public sealed class RenderPipelineIntegrationTests
{
    private static readonly byte[] FakePdfBytes = [0x25, 0x50, 0x44, 0x46]; // %PDF

    private IDocumentPipeline BuildPipeline(Mock<IDocumentRenderer>? rendererMock = null)
    {
        if (rendererMock is null)
        {
            rendererMock = new Mock<IDocumentRenderer>();
            rendererMock
                .Setup(r => r.RenderPdfAsync(It.IsAny<string>(), It.IsAny<PdfOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(FakePdfBytes);
        }

        ITemplateEngine engine = new HandlebarsTemplateEngine(
            NullLogger<HandlebarsTemplateEngine>.Instance);

        return new DocumentPipeline(
            engine,
            rendererMock.Object,
            NullLogger<DocumentPipeline>.Instance);
    }

    // -----------------------------------------------------------------------
    // End-to-end render
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Pipeline_SimpleTemplate_ReturnsPdfBytes()
    {
        var pipeline = BuildPipeline();
        var request  = BuildRequest("<p>Hello</p>");

        var result = await pipeline.ExecuteAsync(request);

        result.PdfBytes.Should().Equal(FakePdfBytes);
    }

    [Fact]
    public async Task Pipeline_TemplateWithVariables_RendererReceivesSubstitutedHtml()
    {
        var rendererMock = new Mock<IDocumentRenderer>();
        string? capturedHtml = null;

        rendererMock
            .Setup(r => r.RenderPdfAsync(It.IsAny<string>(), It.IsAny<PdfOptions>(), It.IsAny<CancellationToken>()))
            .Callback<string, PdfOptions, CancellationToken>((html, _, _) => capturedHtml = html)
            .ReturnsAsync(FakePdfBytes);

        var pipeline = BuildPipeline(rendererMock);

        var request = BuildRequest(
            "<p>{{variables.name}}</p>",
            variables: new() { ["name"] = "Integration Test" });

        await pipeline.ExecuteAsync(request);

        capturedHtml.Should().Contain("Integration Test");
        capturedHtml.Should().NotContain("{{variables.name}}");
    }

    [Fact]
    public async Task Pipeline_TemplateWithBranding_RendererReceivesBrandedHtml()
    {
        var rendererMock = new Mock<IDocumentRenderer>();
        string? capturedHtml = null;

        rendererMock
            .Setup(r => r.RenderPdfAsync(It.IsAny<string>(), It.IsAny<PdfOptions>(), It.IsAny<CancellationToken>()))
            .Callback<string, PdfOptions, CancellationToken>((html, _, _) => capturedHtml = html)
            .ReturnsAsync(FakePdfBytes);

        var pipeline = BuildPipeline(rendererMock);

        var request = BuildRequest(
            "<h1>{{branding.companyName}}</h1>",
            branding: new Branding { CompanyName = "ACME" });

        await pipeline.ExecuteAsync(request);

        capturedHtml.Should().Contain("ACME");
    }

    [Fact]
    public async Task Pipeline_TemplateWithCss_CssIsInjectedIntoHtml()
    {
        var rendererMock = new Mock<IDocumentRenderer>();
        string? capturedHtml = null;

        rendererMock
            .Setup(r => r.RenderPdfAsync(It.IsAny<string>(), It.IsAny<PdfOptions>(), It.IsAny<CancellationToken>()))
            .Callback<string, PdfOptions, CancellationToken>((html, _, _) => capturedHtml = html)
            .ReturnsAsync(FakePdfBytes);

        var pipeline = BuildPipeline(rendererMock);

        var request = new RenderRequest
        {
            Template = new DocumentTemplate
            {
                DocumentType = "test",
                Template = new TemplateContent
                {
                    Html = "<html><head></head><body>content</body></html>",
                    Css  = "body { background: blue; }"
                }
            }
        };

        await pipeline.ExecuteAsync(request);

        capturedHtml.Should().Contain("<style>body { background: blue; }</style>");
        capturedHtml.Should().Contain("</head>");
    }

    [Fact]
    public async Task Pipeline_UpperHelper_IsApplied()
    {
        var rendererMock = new Mock<IDocumentRenderer>();
        string? capturedHtml = null;

        rendererMock
            .Setup(r => r.RenderPdfAsync(It.IsAny<string>(), It.IsAny<PdfOptions>(), It.IsAny<CancellationToken>()))
            .Callback<string, PdfOptions, CancellationToken>((html, _, _) => capturedHtml = html)
            .ReturnsAsync(FakePdfBytes);

        var pipeline = BuildPipeline(rendererMock);

        var request = BuildRequest(
            "{{upper variables.name}}",
            variables: new() { ["name"] = "world" });

        await pipeline.ExecuteAsync(request);

        capturedHtml.Should().Be("WORLD");
    }

    [Fact]
    public async Task Pipeline_MultipleVariables_AllSubstituted()
    {
        var rendererMock = new Mock<IDocumentRenderer>();
        string? capturedHtml = null;

        rendererMock
            .Setup(r => r.RenderPdfAsync(It.IsAny<string>(), It.IsAny<PdfOptions>(), It.IsAny<CancellationToken>()))
            .Callback<string, PdfOptions, CancellationToken>((html, _, _) => capturedHtml = html)
            .ReturnsAsync(FakePdfBytes);

        var pipeline = BuildPipeline(rendererMock);

        var request = BuildRequest(
            "{{variables.first}} {{variables.last}}",
            variables: new() { ["first"] = "Jane", ["last"] = "Doe" });

        await pipeline.ExecuteAsync(request);

        capturedHtml.Should().Be("Jane Doe");
    }

    [Fact]
    public async Task Pipeline_JobId_IsPreservedInResult()
    {
        var pipeline = BuildPipeline();
        var request  = BuildRequest("<p>test</p>");

        var result = await pipeline.ExecuteAsync(request);

        result.JobId.Should().Be(request.JobId);
    }

    [Fact]
    public async Task Pipeline_DocumentType_IsPreservedInResult()
    {
        var pipeline = BuildPipeline();
        var request  = BuildRequest("<p>test</p>", documentType: "certificate");

        var result = await pipeline.ExecuteAsync(request);

        result.DocumentType.Should().Be("certificate");
    }

    [Fact]
    public async Task Pipeline_Partials_AreExpanded()
    {
        var rendererMock = new Mock<IDocumentRenderer>();
        string? capturedHtml = null;

        rendererMock
            .Setup(r => r.RenderPdfAsync(It.IsAny<string>(), It.IsAny<PdfOptions>(), It.IsAny<CancellationToken>()))
            .Callback<string, PdfOptions, CancellationToken>((html, _, _) => capturedHtml = html)
            .ReturnsAsync(FakePdfBytes);

        var pipeline = BuildPipeline(rendererMock);

        var request = new RenderRequest
        {
            Template = new DocumentTemplate
            {
                DocumentType = "test",
                Template = new TemplateContent
                {
                    Html     = "{{> footer}}",
                    Partials = new Dictionary<string, string>
                    {
                        ["footer"] = "<footer>Confidential</footer>"
                    }
                }
            }
        };

        await pipeline.ExecuteAsync(request);

        capturedHtml.Should().Be("<footer>Confidential</footer>");
    }

    // -----------------------------------------------------------------------
    // DI registration smoke test
    // -----------------------------------------------------------------------

    [Fact]
    public void ServiceCollection_AddTemplating_RegistersITemplateEngine()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTemplating();

        using var provider = services.BuildServiceProvider();
        var engine = provider.GetService<ITemplateEngine>();

        engine.Should().NotBeNull().And.BeOfType<HandlebarsTemplateEngine>();
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static RenderRequest BuildRequest(
        string html = "<p>test</p>",
        Dictionary<string, object?>? variables = null,
        Branding? branding = null,
        string documentType = "test") =>
        new()
        {
            Template = new DocumentTemplate
            {
                DocumentType = documentType,
                Branding     = branding ?? new Branding(),
                Variables    = variables ?? [],
                Template     = new TemplateContent { Html = html }
            }
        };
}

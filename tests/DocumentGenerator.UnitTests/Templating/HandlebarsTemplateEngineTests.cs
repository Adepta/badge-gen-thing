using DocumentGenerator.Core.Models;
using DocumentGenerator.Templating;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DocumentGenerator.UnitTests.Templating;

/// <summary>
/// Unit tests for <see cref="HandlebarsTemplateEngine"/>.
/// All tests are fully in-process — no Chromium, no Kafka, no file system.
/// </summary>
public sealed class HandlebarsTemplateEngineTests
{
    private readonly HandlebarsTemplateEngine _sut;

    public HandlebarsTemplateEngineTests()
    {
        _sut = new HandlebarsTemplateEngine(NullLogger<HandlebarsTemplateEngine>.Instance);
    }

    // -----------------------------------------------------------------------
    // Basic rendering
    // -----------------------------------------------------------------------

    [Fact]
    public async Task RenderAsync_SimpleLiteral_ReturnsHtmlUnchanged()
    {
        var template = BuildTemplate("<p>Hello world</p>");

        var result = await _sut.RenderAsync(template);

        result.Should().Be("<p>Hello world</p>");
    }

    [Fact]
    public async Task RenderAsync_VariableSubstitution_InjectsValue()
    {
        var template = BuildTemplate(
            "<p>{{variables.name}}</p>",
            variables: new() { ["name"] = "Alice" });

        var result = await _sut.RenderAsync(template);

        result.Should().Be("<p>Alice</p>");
    }

    [Fact]
    public async Task RenderAsync_BrandingSubstitution_InjectsCompanyName()
    {
        var template = BuildTemplate(
            "<h1>{{branding.companyName}}</h1>",
            branding: new Branding { CompanyName = "Acme Corp" });

        var result = await _sut.RenderAsync(template);

        result.Should().Be("<h1>Acme Corp</h1>");
    }

    [Fact]
    public async Task RenderAsync_MetaFields_ArePresent()
    {
        var template = BuildTemplate(
            "{{meta.documentType}}|{{meta.version}}",
            documentType: "invoice",
            version: "2.0");

        var result = await _sut.RenderAsync(template);

        result.Should().Be("invoice|2.0");
    }

    [Fact]
    public async Task RenderAsync_MetaGeneratedAt_IsIso8601()
    {
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);

        var template = BuildTemplate("{{meta.generatedAt}}");
        var result   = await _sut.RenderAsync(template);

        DateTimeOffset.TryParse(result, out var parsed).Should().BeTrue();
        parsed.Should().BeAfter(before);
        parsed.Should().BeBefore(DateTimeOffset.UtcNow.AddSeconds(5));
    }

    // -----------------------------------------------------------------------
    // CSS injection
    // -----------------------------------------------------------------------

    [Fact]
    public async Task RenderAsync_WithCssAndHeadTag_InjectsCssBeforeCloseHead()
    {
        var template = BuildTemplate(
            html: "<html><head></head><body>content</body></html>",
            css: "body { color: red; }");

        var result = await _sut.RenderAsync(template);

        result.Should().Contain("<style>body { color: red; }</style></head>");
    }

    [Fact]
    public async Task RenderAsync_WithCssButNoHeadTag_PrependsCssStyle()
    {
        var template = BuildTemplate(
            html: "<body>content</body>",
            css: "p { margin: 0; }");

        var result = await _sut.RenderAsync(template);

        result.Should().StartWith("<style>p { margin: 0; }</style>");
    }

    [Fact]
    public async Task RenderAsync_WithNoCss_HtmlNotModified()
    {
        var html     = "<html><head></head><body>content</body></html>";
        var template = BuildTemplate(html: html);

        var result = await _sut.RenderAsync(template);

        result.Should().Be(html);
        result.Should().NotContain("<style>");
    }

    [Fact]
    public async Task RenderAsync_CssWithHandlebarsVariable_SubstitutesBrandColour()
    {
        var template = BuildTemplate(
            html: "<html><head></head><body></body></html>",
            css: "body { color: {{branding.primaryColour}}; }",
            branding: new Branding { PrimaryColour = "#FF0000" });

        var result = await _sut.RenderAsync(template);

        result.Should().Contain("color: #FF0000;");
    }

    [Fact]
    public async Task RenderAsync_CssWithTripleBrace_DoesNotThrow()
    {
        // CSS closing braces used to confuse Handlebars triple-stache parsing
        var template = BuildTemplate(
            html: "<html><head></head><body></body></html>",
            css: ".foo { color: red; }");

        // Must not throw
        var act = async () => await _sut.RenderAsync(template);
        await act.Should().NotThrowAsync();
    }

    // -----------------------------------------------------------------------
    // Built-in helpers
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("hello", "HELLO")]
    [InlineData("World", "WORLD")]
    [InlineData("", "")]
    public async Task Helper_Upper_ConvertsToUpperCase(string input, string expected)
    {
        var template = BuildTemplate(
            "{{upper variables.val}}",
            variables: new() { ["val"] = input });

        var result = await _sut.RenderAsync(template);

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("HELLO", "hello")]
    [InlineData("World", "world")]
    [InlineData("", "")]
    public async Task Helper_Lower_ConvertsToLowerCase(string input, string expected)
    {
        var template = BuildTemplate(
            "{{lower variables.val}}",
            variables: new() { ["val"] = input });

        var result = await _sut.RenderAsync(template);

        result.Should().Be(expected);
    }

    [Fact]
    public async Task Helper_FormatDate_FormatsWithSuppliedFormat()
    {
        var template = BuildTemplate(
            "{{formatDate variables.date \"dd MMM yyyy\"}}",
            variables: new() { ["date"] = "2026-03-15T00:00:00Z" });

        var result = await _sut.RenderAsync(template);

        result.Should().Be("15 Mar 2026");
    }

    [Fact]
    public async Task Helper_FormatDate_InvalidInput_RendersEmpty()
    {
        var template = BuildTemplate(
            "{{formatDate variables.date \"dd MMM yyyy\"}}",
            variables: new() { ["date"] = "not-a-date" });

        var result = await _sut.RenderAsync(template);

        result.Should().Be(string.Empty);
    }

    [Fact]
    public async Task Helper_Currency_FormatsDecimalWithCulture()
    {
        var template = BuildTemplate(
            "{{currency variables.amount \"en-GB\"}}",
            variables: new() { ["amount"] = "9.99" });

        var result = await _sut.RenderAsync(template);

        // £9.99 in en-GB
        result.Should().Be("£9.99");
    }

    [Fact]
    public async Task Helper_IfEquals_RendersTemplateWhenEqual()
    {
        var template = BuildTemplate(
            "{{#ifEquals variables.role \"admin\"}}YES{{/ifEquals}}",
            variables: new() { ["role"] = "admin" });

        var result = await _sut.RenderAsync(template);

        result.Should().Be("YES");
    }

    [Fact]
    public async Task Helper_IfEquals_RendersInverseWhenNotEqual()
    {
        var template = BuildTemplate(
            "{{#ifEquals variables.role \"admin\"}}YES{{else}}NO{{/ifEquals}}",
            variables: new() { ["role"] = "user" });

        var result = await _sut.RenderAsync(template);

        result.Should().Be("NO");
    }

    // -----------------------------------------------------------------------
    // Partials
    // -----------------------------------------------------------------------

    [Fact]
    public async Task RenderAsync_WithPartial_ExpandsPartialContent()
    {
        var template = new DocumentTemplate
        {
            DocumentType = "test",
            Template = new TemplateContent
            {
                Html     = "{{> greeting}}",
                Partials = new Dictionary<string, string> { ["greeting"] = "<p>Hello from partial</p>" }
            }
        };

        var result = await _sut.RenderAsync(template);

        result.Should().Be("<p>Hello from partial</p>");
    }

    // -----------------------------------------------------------------------
    // Branding custom dictionary
    // -----------------------------------------------------------------------

    [Fact]
    public async Task RenderAsync_BrandingCustomKey_IsAccessible()
    {
        var template = BuildTemplate(
            "<span>{{branding.custom.accentColour}}</span>",
            branding: new Branding
            {
                Custom = new Dictionary<string, string> { ["accentColour"] = "#FF5A5F" }
            });

        var result = await _sut.RenderAsync(template);

        result.Should().Be("<span>#FF5A5F</span>");
    }

    // -----------------------------------------------------------------------
    // QR code helper
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Helper_QrCode_EmitsSvgElement()
    {
        var template = BuildTemplate("{{{qrCode variables.id}}}",
            variables: new() { ["id"] = "TC2026-00842" });

        var result = await _sut.RenderAsync(template);

        result.Should().Contain("<svg");
        result.Should().Contain("</svg>");
    }

    [Fact]
    public async Task Helper_QrCode_EmptyValue_EmitsNothing()
    {
        var template = BuildTemplate("{{{qrCode variables.id}}}",
            variables: new() { ["id"] = "" });

        var result = await _sut.RenderAsync(template);

        result.Should().NotContain("<svg");
    }

    [Fact]
    public async Task Helper_QrCode_NullValue_EmitsNothing()
    {
        var template = BuildTemplate("{{{qrCode variables.id}}}",
            variables: new() { ["id"] = null });

        var result = await _sut.RenderAsync(template);

        result.Should().NotContain("<svg");
    }

    [Fact]
    public async Task Helper_QrCode_CustomDarkColour_AppearsInSvg()
    {
        var template = BuildTemplate(
            "{{{qrCode variables.id \"#D4AF37\" \"transparent\"}}}",
            variables: new() { ["id"] = "EXEC-001" });

        var result = await _sut.RenderAsync(template);

        result.Should().Contain("#D4AF37");
    }

    [Fact]
    public async Task Helper_QrCode_TransparentLight_NoWhiteFill()
    {
        var template = BuildTemplate(
            "{{{qrCode variables.id \"#000000\" \"transparent\"}}}",
            variables: new() { ["id"] = "BADGE-XYZ" });

        var result = await _sut.RenderAsync(template);

        // Background rect should be transparent (none), not white
        result.Should().NotMatchRegex("fill=\"#ffffff\"", "white fill should have been replaced");
        result.Should().NotMatchRegex("fill=\"white\"",   "white fill should have been replaced");
    }

    // -----------------------------------------------------------------------
    // Barcode helper
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Helper_BarCode_EmitsSvgElement()
    {
        var template = BuildTemplate("{{{barCode variables.id}}}",
            variables: new() { ["id"] = "TC2026-00842" });

        var result = await _sut.RenderAsync(template);

        result.Should().Contain("<svg");
        result.Should().Contain("</svg>");
    }

    [Fact]
    public async Task Helper_BarCode_EmptyValue_EmitsNothing()
    {
        var template = BuildTemplate("{{{barCode variables.id}}}",
            variables: new() { ["id"] = "" });

        var result = await _sut.RenderAsync(template);

        result.Should().NotContain("<svg");
    }

    [Fact]
    public async Task Helper_BarCode_NullValue_EmitsNothing()
    {
        var template = BuildTemplate("{{{barCode variables.id}}}",
            variables: new() { ["id"] = null });

        var result = await _sut.RenderAsync(template);

        result.Should().NotContain("<svg");
    }

    [Fact]
    public async Task Helper_BarCode_CustomColour_AppearsInSvg()
    {
        var template = BuildTemplate(
            "{{{barCode variables.id \"40\" \"false\" \"#A3E635\"}}}",
            variables: new() { ["id"] = "GJ26-0391" });

        var result = await _sut.RenderAsync(template);

        result.Should().Contain("<svg");
        result.Should().Contain("#A3E635");
    }

    [Fact]
    public async Task Helper_BarCode_DifferentFromQrCode()
    {
        var id = "COMPARE-001";
        var qrTemplate  = BuildTemplate("{{{qrCode variables.id}}}",  variables: new() { ["id"] = id });
        var barTemplate = BuildTemplate("{{{barCode variables.id}}}", variables: new() { ["id"] = id });

        var qrResult  = await _sut.RenderAsync(qrTemplate);
        var barResult = await _sut.RenderAsync(barTemplate);

        // Both produce SVG but the content must differ (different symbologies)
        qrResult.Should().Contain("<svg");
        barResult.Should().Contain("<svg");
        qrResult.Should().NotBe(barResult);
    }

    // -----------------------------------------------------------------------
    // Cancellation
    // -----------------------------------------------------------------------

    [Fact]
    public async Task RenderAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        var cts      = new CancellationTokenSource();
        cts.Cancel();

        var template = BuildTemplate("<p>test</p>");

        var act = async () => await _sut.RenderAsync(template, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static DocumentTemplate BuildTemplate(
        string html = "<p>test</p>",
        string? css = null,
        Dictionary<string, object?>? variables = null,
        Branding? branding = null,
        string documentType = "test",
        string version = "1.0") =>
        new()
        {
            DocumentType = documentType,
            Version      = version,
            Branding     = branding ?? new Branding(),
            Variables    = variables ?? [],
            Template     = new TemplateContent
            {
                Html = html,
                Css  = css
            }
        };
}

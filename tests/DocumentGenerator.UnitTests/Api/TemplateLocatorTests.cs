using DocumentGenerator.Api.Services;
using DocumentGenerator.Core.Errors;
using DocumentGenerator.Core.Models;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DocumentGenerator.UnitTests.Api;

/// <summary>
/// Unit tests for <see cref="TemplateLocator"/>.
/// Uses a real temp directory with dummy HTML/CSS files so no actual templates
/// directory is required, keeping tests fully self-contained.
/// </summary>
public sealed class TemplateLocatorTests : IDisposable
{
    private readonly string         _tempDir;
    private readonly TemplateLocator _sut;

    public TemplateLocatorTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"tpl_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        // Create dummy template files
        File.WriteAllText(Path.Combine(_tempDir, "badge-pulse-a6.html"), "<p>pulse a6</p>");
        File.WriteAllText(Path.Combine(_tempDir, "badge-pulse-a6.css"),  "body{}");
        File.WriteAllText(Path.Combine(_tempDir, "badge-executive-cc.html"), "<p>exec cc</p>");
        // No CSS file for exec-cc — tests optional CSS
        File.WriteAllText(Path.Combine(_tempDir, "badge-plain.html"), "<p>plain</p>");

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DocumentGenerator:TemplatesPath"] = _tempDir
            })
            .Build();

        _sut = new TemplateLocator(config, NullLogger<TemplateLocator>.Instance);
    }

    // ── Resolve ───────────────────────────────────────────────────────────────

    [Fact]
    public void Resolve_KnownTemplate_ReturnsDocumentTemplate()
    {
        var result = _sut.Resolve("badge-pulse-a6", []);
        result.Should().NotBeNull();
    }

    [Fact]
    public void Resolve_SetsDocumentTypeToBadge()
    {
        var result = _sut.Resolve("badge-pulse-a6", []);
        result.DocumentType.Should().Be("badge");
    }

    [Fact]
    public void Resolve_SetsHtmlPath()
    {
        var result = _sut.Resolve("badge-pulse-a6", []);
        result.Template.HtmlPath.Should().EndWith("badge-pulse-a6.html");
    }

    [Fact]
    public void Resolve_SetsCssPathWhenCssFileExists()
    {
        var result = _sut.Resolve("badge-pulse-a6", []);
        result.Template.CssPath.Should().EndWith("badge-pulse-a6.css");
    }

    [Fact]
    public void Resolve_NoCssFile_CssPathIsNull()
    {
        var result = _sut.Resolve("badge-executive-cc", []);
        result.Template.CssPath.Should().BeNull();
    }

    [Fact]
    public void Resolve_InjectsVariables()
    {
        var vars   = new Dictionary<string, object?> { ["firstName"] = "Jane" };
        var result = _sut.Resolve("badge-pulse-a6", vars);
        result.Variables.Should().ContainKey("firstName");
        result.Variables["firstName"].Should().Be("Jane");
    }

    [Fact]
    public void Resolve_UsesBrandingWhenProvided()
    {
        var branding = new Branding { CompanyName = "Acme" };
        var result   = _sut.Resolve("badge-pulse-a6", [], branding);
        result.Branding.CompanyName.Should().Be("Acme");
    }

    [Fact]
    public void Resolve_DefaultsBrandingWhenNull()
    {
        var result = _sut.Resolve("badge-pulse-a6", [], null);
        result.Branding.Should().NotBeNull();
    }

    [Fact]
    public void Resolve_UnknownTemplate_ThrowsTemplateException()
    {
        var act = () => _sut.Resolve("does-not-exist", []);
        act.Should().Throw<TemplateException>()
            .Which.Code.Should().Be(ErrorCode.TemplateNotFound);
    }

    // ── PDF options by suffix ─────────────────────────────────────────────────

    [Fact]
    public void Resolve_A6Suffix_Sets105x148Dimensions()
    {
        var result = _sut.Resolve("badge-pulse-a6", []);
        result.Pdf.Width.Should().Be("105mm");
        result.Pdf.Height.Should().Be("148mm");
    }

    [Fact]
    public void Resolve_CcSuffix_Sets85x54Dimensions()
    {
        var result = _sut.Resolve("badge-executive-cc", []);
        result.Pdf.Width.Should().Be("85.6mm");
        result.Pdf.Height.Should().Be("54mm");
    }

    [Fact]
    public void Resolve_NoSuffix_SetsA4Format()
    {
        var result = _sut.Resolve("badge-plain", []);
        result.Pdf.Format.Should().Be("A4");
        result.Pdf.Width.Should().BeNull();
    }

    [Fact]
    public void Resolve_A6Suffix_HasZeroMargins()
    {
        var result = _sut.Resolve("badge-pulse-a6", []);
        result.Pdf.Margins.Should().NotBeNull();
        result.Pdf.Margins!.Top.Should().Be("0mm");
        result.Pdf.Margins.Left.Should().Be("0mm");
    }

    // ── ListTemplates ─────────────────────────────────────────────────────────

    [Fact]
    public void ListTemplates_ReturnsAllHtmlFileNames()
    {
        var templates = _sut.ListTemplates().ToList();
        templates.Should().Contain("badge-pulse-a6");
        templates.Should().Contain("badge-executive-cc");
        templates.Should().Contain("badge-plain");
    }

    [Fact]
    public void ListTemplates_ExcludesSampleFiles()
    {
        File.WriteAllText(Path.Combine(_tempDir, "sample-badge.html"), "<p/>");
        var templates = _sut.ListTemplates().ToList();
        templates.Should().NotContain("sample-badge");
    }

    [Fact]
    public void ListTemplates_EmptyDirectory_ReturnsEmpty()
    {
        var emptyDir = Path.Combine(Path.GetTempPath(), $"empty_{Guid.NewGuid():N}");
        Directory.CreateDirectory(emptyDir);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DocumentGenerator:TemplatesPath"] = emptyDir
            })
            .Build();

        var locator   = new TemplateLocator(config, NullLogger<TemplateLocator>.Instance);
        var templates = locator.ListTemplates();

        templates.Should().BeEmpty();
        Directory.Delete(emptyDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* best-effort */ }
    }
}

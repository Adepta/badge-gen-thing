using DocumentGenerator.Api.Services;
using DocumentGenerator.Core.Errors;
using DocumentGenerator.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
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
        result.ShouldNotBeNull();
    }

    [Fact]
    public void Resolve_SetsDocumentTypeToBadge()
    {
        var result = _sut.Resolve("badge-pulse-a6", []);
        result.DocumentType.ShouldBe("badge");
    }

    [Fact]
    public void Resolve_SetsHtmlPath()
    {
        var result = _sut.Resolve("badge-pulse-a6", []);
        result.Template.HtmlPath.ShouldEndWith("badge-pulse-a6.html");
    }

    [Fact]
    public void Resolve_SetsCssPathWhenCssFileExists()
    {
        var result = _sut.Resolve("badge-pulse-a6", []);
        result.Template.CssPath.ShouldEndWith("badge-pulse-a6.css");
    }

    [Fact]
    public void Resolve_NoCssFile_CssPathIsNull()
    {
        var result = _sut.Resolve("badge-executive-cc", []);
        result.Template.CssPath.ShouldBeNull();
    }

    [Fact]
    public void Resolve_InjectsVariables()
    {
        var vars   = new Dictionary<string, object?> { ["firstName"] = "Jane" };
        var result = _sut.Resolve("badge-pulse-a6", vars);
        result.Variables.ShouldContainKey("firstName");
        result.Variables["firstName"].ShouldBe("Jane");
    }

    [Fact]
    public void Resolve_UsesBrandingWhenProvided()
    {
        var branding = new Branding { CompanyName = "Acme" };
        var result   = _sut.Resolve("badge-pulse-a6", [], branding);
        result.Branding.CompanyName.ShouldBe("Acme");
    }

    [Fact]
    public void Resolve_DefaultsBrandingWhenNull()
    {
        var result = _sut.Resolve("badge-pulse-a6", [], null);
        result.Branding.ShouldNotBeNull();
    }

    [Fact]
    public void Resolve_UnknownTemplate_ThrowsTemplateException()
    {
        var ex = Should.Throw<TemplateException>(() => _sut.Resolve("does-not-exist", []));
        ex.Code.ShouldBe(ErrorCode.TemplateNotFound);
    }

    // ── PDF options by suffix ─────────────────────────────────────────────────

    [Fact]
    public void Resolve_A6Suffix_Sets105x148Dimensions()
    {
        var result = _sut.Resolve("badge-pulse-a6", []);
        result.Pdf.Width.ShouldBe("105mm");
        result.Pdf.Height.ShouldBe("148mm");
    }

    [Fact]
    public void Resolve_CcSuffix_Sets85x54Dimensions()
    {
        var result = _sut.Resolve("badge-executive-cc", []);
        result.Pdf.Width.ShouldBe("85.6mm");
        result.Pdf.Height.ShouldBe("54mm");
    }

    [Fact]
    public void Resolve_NoSuffix_SetsA4Format()
    {
        var result = _sut.Resolve("badge-plain", []);
        result.Pdf.Format.ShouldBe("A4");
        result.Pdf.Width.ShouldBeNull();
    }

    [Fact]
    public void Resolve_A6Suffix_HasZeroMargins()
    {
        var result = _sut.Resolve("badge-pulse-a6", []);
        result.Pdf.Margins.ShouldNotBeNull();
        result.Pdf.Margins!.Top.ShouldBe("0mm");
        result.Pdf.Margins.Left.ShouldBe("0mm");
    }

    // ── ListTemplates ─────────────────────────────────────────────────────────

    [Fact]
    public void ListTemplates_ReturnsAllHtmlFileNames()
    {
        var templates = _sut.ListTemplates().ToList();
        templates.ShouldContain("badge-pulse-a6");
        templates.ShouldContain("badge-executive-cc");
        templates.ShouldContain("badge-plain");
    }

    [Fact]
    public void ListTemplates_ExcludesSampleFiles()
    {
        File.WriteAllText(Path.Combine(_tempDir, "sample-badge.html"), "<p/>");
        var templates = _sut.ListTemplates().ToList();
        templates.ShouldNotContain("sample-badge");
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

        templates.ShouldBeEmpty();
        Directory.Delete(emptyDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* best-effort */ }
    }
}

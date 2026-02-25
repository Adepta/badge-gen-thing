using DocumentGenerator.Core.Models;
using DocumentGenerator.Templating;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DocumentGenerator.UnitTests.Templating;

/// <summary>
/// Unit tests for <see cref="FileTemplateContentResolver"/>.
/// Files are written to a temp directory so no real project files are touched.
/// </summary>
public sealed class FileTemplateContentResolverTests : IDisposable
{
    private readonly FileTemplateContentResolver _sut;
    private readonly string _tempDir;

    public FileTemplateContentResolverTests()
    {
        _sut     = new FileTemplateContentResolver(NullLogger<FileTemplateContentResolver>.Instance);
        _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    // -----------------------------------------------------------------------
    // No paths set — passthrough
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ResolveAsync_NoPaths_ReturnsSameTemplateInstance()
    {
        var template = BuildTemplate(html: "<p>inline</p>", css: "body{}");

        var result = await _sut.ResolveAsync(template, _tempDir);

        result.Should().BeSameAs(template, "no paths means nothing to resolve");
    }

    [Fact]
    public async Task ResolveAsync_NoPaths_InlineHtmlPreserved()
    {
        var template = BuildTemplate(html: "<p>hello</p>");

        var result = await _sut.ResolveAsync(template, _tempDir);

        result.Template.Html.Should().Be("<p>hello</p>");
    }

    // -----------------------------------------------------------------------
    // HtmlPath resolution
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ResolveAsync_RelativeHtmlPath_LoadsContentFromFile()
    {
        var htmlContent = "<section>from file</section>";
        WriteFile("page.html", htmlContent);

        var template = BuildTemplate(htmlPath: "page.html");

        var result = await _sut.ResolveAsync(template, _tempDir);

        result.Template.Html.Should().Be(htmlContent);
    }

    [Fact]
    public async Task ResolveAsync_AbsoluteHtmlPath_LoadsContentFromFile()
    {
        var htmlContent = "<h1>absolute</h1>";
        var absolutePath = WriteFile("abs.html", htmlContent);

        var template = BuildTemplate(htmlPath: absolutePath);

        var result = await _sut.ResolveAsync(template, basePath: @"C:\some\other\dir");

        result.Template.Html.Should().Be(htmlContent);
    }

    [Fact]
    public async Task ResolveAsync_HtmlPath_CssPath_Null_CssRemainsNull()
    {
        WriteFile("only.html", "<p/>");

        var template = BuildTemplate(htmlPath: "only.html");

        var result = await _sut.ResolveAsync(template, _tempDir);

        result.Template.Css.Should().BeNull();
    }

    // -----------------------------------------------------------------------
    // CssPath resolution
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ResolveAsync_RelativeCssPath_LoadsContentFromFile()
    {
        var cssContent = "body { margin: 0; }";
        WriteFile("styles.css", cssContent);

        var template = BuildTemplate(cssPath: "styles.css");

        var result = await _sut.ResolveAsync(template, _tempDir);

        result.Template.Css.Should().Be(cssContent);
    }

    [Fact]
    public async Task ResolveAsync_CssPath_HtmlPath_NotSet_HtmlRemainsDefault()
    {
        WriteFile("only.css", "p{}");

        var template = BuildTemplate(cssPath: "only.css");

        var result = await _sut.ResolveAsync(template, _tempDir);

        // Html defaults to string.Empty when no htmlPath / inline html is given
        result.Template.Html.Should().Be(string.Empty);
    }

    // -----------------------------------------------------------------------
    // Both paths set
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ResolveAsync_BothPaths_LoadsBothFiles()
    {
        var htmlContent = "<html><body>hi</body></html>";
        var cssContent  = "html { font-size: 16px; }";
        WriteFile("tmpl.html", htmlContent);
        WriteFile("tmpl.css",  cssContent);

        var template = BuildTemplate(htmlPath: "tmpl.html", cssPath: "tmpl.css");

        var result = await _sut.ResolveAsync(template, _tempDir);

        result.Template.Html.Should().Be(htmlContent);
        result.Template.Css.Should().Be(cssContent);
    }

    [Fact]
    public async Task ResolveAsync_BothPaths_PathPropertiesPreservedOnResult()
    {
        WriteFile("a.html", "<p/>");
        WriteFile("a.css",  "p{}");

        var template = BuildTemplate(htmlPath: "a.html", cssPath: "a.css");

        var result = await _sut.ResolveAsync(template, _tempDir);

        result.Template.HtmlPath.Should().Be("a.html");
        result.Template.CssPath.Should().Be("a.css");
    }

    [Fact]
    public async Task ResolveAsync_BothPaths_ReturnsNewTemplateInstance()
    {
        WriteFile("b.html", "<p/>");
        WriteFile("b.css",  "p{}");

        var template = BuildTemplate(htmlPath: "b.html", cssPath: "b.css");

        var result = await _sut.ResolveAsync(template, _tempDir);

        result.Should().NotBeSameAs(template, "a new instance is returned with resolved content");
    }

    // -----------------------------------------------------------------------
    // Template metadata preserved
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ResolveAsync_HtmlPath_NonTemplatePropertiesPreserved()
    {
        WriteFile("meta.html", "<p/>");

        var branding = new Branding { CompanyName = "Acme", PrimaryColour = "#abc" };
        var vars     = new Dictionary<string, object?> { ["key"] = "value" };
        var pdf      = new PdfOptions { Width = "85mm", Height = "54mm" };

        var template = new DocumentTemplate
        {
            DocumentType = "badge",
            Version      = "3.0",
            Branding     = branding,
            Variables    = vars,
            Pdf          = pdf,
            Template     = new TemplateContent { HtmlPath = "meta.html" }
        };

        var result = await _sut.ResolveAsync(template, _tempDir);

        result.DocumentType.Should().Be("badge");
        result.Version.Should().Be("3.0");
        result.Branding.Should().BeSameAs(branding);
        result.Variables.Should().BeSameAs(vars);
        result.Pdf.Should().BeSameAs(pdf);
    }

    // -----------------------------------------------------------------------
    // Cancellation
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ResolveAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        WriteFile("cancel.html", "<p/>");

        var cts      = new CancellationTokenSource();
        cts.Cancel();

        var template = BuildTemplate(htmlPath: "cancel.html");

        var act = async () => await _sut.ResolveAsync(template, _tempDir, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // -----------------------------------------------------------------------
    // Error paths
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ResolveAsync_HtmlPath_FileNotFound_ThrowsFileNotFoundException()
    {
        var template = BuildTemplate(htmlPath: "nonexistent.html");

        var act = async () => await _sut.ResolveAsync(template, _tempDir);

        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    [Fact]
    public async Task ResolveAsync_CssPath_FileNotFound_ThrowsFileNotFoundException()
    {
        var template = BuildTemplate(cssPath: "nonexistent.css");

        var act = async () => await _sut.ResolveAsync(template, _tempDir);

        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    /// <summary>Writes a file in the temp directory and returns its full path.</summary>
    private string WriteFile(string name, string content)
    {
        var path = Path.Combine(_tempDir, name);
        File.WriteAllText(path, content);
        return path;
    }

    private static DocumentTemplate BuildTemplate(
        string  html     = "",
        string? css      = null,
        string? htmlPath = null,
        string? cssPath  = null) =>
        new()
        {
            DocumentType = "test",
            Template = new TemplateContent
            {
                Html     = html,
                Css      = css,
                HtmlPath = htmlPath,
                CssPath  = cssPath
            }
        };
}

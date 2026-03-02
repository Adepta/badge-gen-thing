using DocumentGenerator.Api.Services;
using DocumentGenerator.Core.Errors;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace DocumentGenerator.UnitTests.Api;

/// <summary>
/// Tests that <see cref="TemplateLocator"/> rejects path traversal attempts.
/// The tests do NOT rely on the filesystem — we verify that the guard throws before
/// any file I/O takes place.
/// </summary>
public sealed class TemplateLocatorPathTraversalTests : IDisposable
{
    private readonly string _tempDir;
    private readonly TemplateLocator _sut;

    public TemplateLocatorPathTraversalTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"tpl_pt_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        // Create a legitimate template so the locator initialises cleanly.
        File.WriteAllText(Path.Combine(_tempDir, "badge-ok.html"), "<p/>");

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DocumentGenerator:TemplatesPath"] = _tempDir
            })
            .Build();

        _sut = new TemplateLocator(config, NullLogger<TemplateLocator>.Instance);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* best-effort */ }
    }

    // ── Relative path traversal ───────────────────────────────────────────────

    [Fact]
    public void Resolve_RelativeTraversal_ThrowsTemplateNameInvalid()
    {
        // "../etc/passwd" should be detected as escaping the templates directory
        var act = () => _sut.Resolve("../etc/passwd", []);

        var ex = Should.Throw<TemplateException>(() => act());
        ex.Code.ShouldBe(ErrorCode.TemplateNameInvalid);
    }

    [Fact]
    public void Resolve_DeepRelativeTraversal_ThrowsTemplateNameInvalid()
    {
        var act = () => _sut.Resolve("../../secret", []);

        var ex = Should.Throw<TemplateException>(() => act());
        ex.Code.ShouldBe(ErrorCode.TemplateNameInvalid);
    }

    // ── Absolute path injection ───────────────────────────────────────────────

    [Fact]
    public void Resolve_AbsoluteWindowsPath_ThrowsTemplateNameInvalid()
    {
        // On Windows, Path.Combine with an absolute path replaces the base — the guard catches it.
        var act = () => _sut.Resolve("C:\\Windows\\System32\\drivers\\etc\\hosts", []);

        var ex = Should.Throw<TemplateException>(() => act());
        ex.Code.ShouldBe(ErrorCode.TemplateNameInvalid);
    }

    [Fact]
    public void Resolve_AbsoluteUnixPath_ThrowsTemplateNameInvalid()
    {
        // /etc/passwd as template name
        var act = () => _sut.Resolve("/etc/passwd", []);

        var ex = Should.Throw<TemplateException>(() => act());
        ex.Code.ShouldBe(ErrorCode.TemplateNameInvalid);
    }

    // ── Directory name prefix bypass ──────────────────────────────────────────

    [Fact]
    public void Resolve_SiblingDirectoryPrefixBypass_ThrowsTemplateNameInvalid()
    {
        // If templates dir is /tmp/tpl_XXX, a name like "../tpl_XXXevil/secret" must not
        // match the prefix check.  The trailing separator on safeRoot prevents this.
        var act = () => _sut.Resolve($"../tpl_bypass/secret", []);

        var ex = Should.Throw<TemplateException>(() => act());
        ex.Code.ShouldBe(ErrorCode.TemplateNameInvalid);
    }

    // ── Legitimate template name ───────────────────────────────────────────────

    [Fact]
    public void Resolve_ValidTemplateName_DoesNotThrowNameInvalid()
    {
        // badge-ok.html exists — should throw TemplateNotFound at worst, never TemplateNameInvalid
        // (This also documents that a normal name is accepted by the guard.)
        Should.NotThrow(() => _sut.Resolve("badge-ok", []));
    }
}

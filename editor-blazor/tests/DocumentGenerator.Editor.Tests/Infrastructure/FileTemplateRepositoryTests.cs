using FluentAssertions;
using DocumentGenerator.Editor.Infrastructure.FileSystem;
using DocumentGenerator.Editor.Core.Models;

namespace DocumentGenerator.Editor.Tests.Infrastructure;

public class FileTemplateRepositoryTests : IDisposable
{
    private readonly string _tempDir;
    private readonly FileTemplateRepository _repo;

    public FileTemplateRepositoryTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"editor-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _repo = new FileTemplateRepository(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task ListAsync_ReturnsHtmlCssPairs()
    {
        // Arrange
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "badge-pulse-a6.html"), "<div>Pulse</div>");
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "badge-pulse-a6.css"), ".badge { }");
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "badge-carbon-cc.html"), "<div>Carbon</div>");

        // Act
        var items = await _repo.ListAsync();

        // Assert
        items.Should().HaveCount(2);
        var pulseItem = items.Should().ContainSingle(i => i.Name == "badge-pulse-a6").Subject;
        pulseItem.HasCss.Should().BeTrue();
        pulseItem.Family.Should().Be(TemplateFamily.Pulse);
        pulseItem.SizePreset.Should().Be(SizePreset.A6);

        var carbonItem = items.Should().ContainSingle(i => i.Name == "badge-carbon-cc").Subject;
        carbonItem.HasCss.Should().BeFalse();
        carbonItem.Family.Should().Be(TemplateFamily.Carbon);
        carbonItem.SizePreset.Should().Be(SizePreset.CreditCard);
    }

    [Fact]
    public async Task GetAsync_ReadsContent()
    {
        // Arrange
        var htmlContent = "<div>Hello {{variables.firstName}}</div>";
        var cssContent = ".badge { color: red; }";
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "test-template.html"), htmlContent);
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "test-template.css"), cssContent);

        // Act
        var template = await _repo.GetAsync("test-template");

        // Assert
        template.Should().NotBeNull();
        template!.Name.Should().Be("test-template");
        template.HtmlContent.Should().Be(htmlContent);
        template.CssContent.Should().Be(cssContent);
    }

    [Fact]
    public async Task GetAsync_NonExistent_ReturnsNull()
    {
        var result = await _repo.GetAsync("does-not-exist");
        result.Should().BeNull();
    }

    [Fact]
    public async Task SaveAsync_WritesFiles()
    {
        // Act
        await _repo.SaveAsync("new-template", "<h1>Title</h1>", "h1 { font-size: 2em; }");

        // Assert
        File.Exists(Path.Combine(_tempDir, "new-template.html")).Should().BeTrue();
        File.Exists(Path.Combine(_tempDir, "new-template.css")).Should().BeTrue();

        var html = await File.ReadAllTextAsync(Path.Combine(_tempDir, "new-template.html"));
        html.Should().Be("<h1>Title</h1>");

        var css = await File.ReadAllTextAsync(Path.Combine(_tempDir, "new-template.css"));
        css.Should().Be("h1 { font-size: 2em; }");
    }

    [Fact]
    public async Task DeleteAsync_RemovesAllRelatedFiles()
    {
        // Arrange
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "to-delete.html"), "<div>Delete me</div>");
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "to-delete.css"), ".del { }");
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "sample-to-delete.json"), "{}");

        // Act
        await _repo.DeleteAsync("to-delete");

        // Assert
        File.Exists(Path.Combine(_tempDir, "to-delete.html")).Should().BeFalse();
        File.Exists(Path.Combine(_tempDir, "to-delete.css")).Should().BeFalse();
        File.Exists(Path.Combine(_tempDir, "sample-to-delete.json")).Should().BeFalse();
    }

    [Fact]
    public async Task RenameAsync_RenamesAllFiles()
    {
        // Arrange
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "old-name.html"), "<div>Old</div>");
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "old-name.css"), ".old { }");
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "sample-old-name.json"), "{}");

        // Act
        await _repo.RenameAsync("old-name", "new-name");

        // Assert
        File.Exists(Path.Combine(_tempDir, "old-name.html")).Should().BeFalse();
        File.Exists(Path.Combine(_tempDir, "old-name.css")).Should().BeFalse();
        File.Exists(Path.Combine(_tempDir, "sample-old-name.json")).Should().BeFalse();

        File.Exists(Path.Combine(_tempDir, "new-name.html")).Should().BeTrue();
        File.Exists(Path.Combine(_tempDir, "new-name.css")).Should().BeTrue();
        File.Exists(Path.Combine(_tempDir, "sample-new-name.json")).Should().BeTrue();

        var html = await File.ReadAllTextAsync(Path.Combine(_tempDir, "new-name.html"));
        html.Should().Be("<div>Old</div>");
    }

    [Fact]
    public async Task ExistsAsync_ReturnsTrueForExisting()
    {
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "exists-test.html"), "<div />");

        var result = await _repo.ExistsAsync("exists-test");
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_ReturnsFalseForNonExistent()
    {
        var result = await _repo.ExistsAsync("no-such-template");
        result.Should().BeFalse();
    }

    [Fact]
    public void SafePath_RejectsPathTraversal()
    {
        // Path traversal via GetAsync should throw
        var act = () => _repo.GetAsync("../../../etc/passwd");
        act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*traversal*");
    }

    [Fact]
    public void SaveAsync_RejectsInvalidCharacters()
    {
        var act = () => _repo.SaveAsync("../bad-name", "<div />", "");
        act.Should().ThrowAsync<ArgumentException>();
    }
}

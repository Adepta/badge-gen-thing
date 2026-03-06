using FluentAssertions;
using DocumentGenerator.Editor.Core.DTOs;
using DocumentGenerator.Editor.Core.Models;
using DocumentGenerator.Editor.Infrastructure.Database;
using DocumentGenerator.Editor.Infrastructure.FileSystem;
using DocumentGenerator.Editor.Infrastructure.Services;

namespace DocumentGenerator.Editor.Tests.Services;

public class TemplateServiceTests : IAsyncLifetime
{
    private readonly string _tempDir;
    private readonly FileTemplateRepository _fileRepo;
    private readonly SampleDataService _sampleDataService;
    private readonly SqliteMetadataStore _metadataStore;
    private readonly TemplateService _service;

    public TemplateServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"editor-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        _fileRepo = new FileTemplateRepository(_tempDir);
        _sampleDataService = new SampleDataService(_tempDir);

        var dbPath = Path.Combine(Path.GetTempPath(), $"editor-test-{Guid.NewGuid():N}.db");
        _metadataStore = new SqliteMetadataStore($"Data Source={dbPath}");

        _service = new TemplateService(_fileRepo, _metadataStore, _sampleDataService);
    }

    public async Task InitializeAsync()
    {
        await _metadataStore.InitializeAsync();
    }

    public Task DisposeAsync()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
        return Task.CompletedTask;
    }

    [Fact]
    public async Task ListAsync_ReturnsTemplates()
    {
        // Arrange - create template files and sync metadata
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "badge-pulse-a6.html"), "<div />");
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "badge-pulse-a6.css"), "");
        await _service.SyncMetadataAsync();

        // Act
        var items = await _service.ListAsync();

        // Assert
        items.Should().NotBeEmpty();
        items.Should().ContainSingle(i => i.Name == "badge-pulse-a6");
    }

    [Fact]
    public async Task GetAsync_ExistingTemplate_ReturnsContent()
    {
        // Arrange
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "get-test.html"), "<h1>Hello</h1>");
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "get-test.css"), "h1 { color: blue; }");

        // Act
        var template = await _service.GetAsync("get-test");

        // Assert
        template.Should().NotBeNull();
        template!.HtmlContent.Should().Be("<h1>Hello</h1>");
        template.CssContent.Should().Be("h1 { color: blue; }");
    }

    [Fact]
    public async Task GetAsync_NonExistent_ReturnsNull()
    {
        var result = await _service.GetAsync("nonexistent");
        result.Should().BeNull();
    }

    [Fact]
    public async Task SaveAsync_WritesFiles()
    {
        // Act
        var request = new TemplateSaveRequest("save-test", "<div>Saved</div>", ".saved { }");
        await _service.SaveAsync(request);

        // Assert
        var template = await _fileRepo.GetAsync("save-test");
        template.Should().NotBeNull();
        template!.HtmlContent.Should().Be("<div>Saved</div>");
        template.CssContent.Should().Be(".saved { }");

        // Metadata should also be saved
        var metadata = await _metadataStore.SearchAsync(query: "save-test");
        metadata.Should().ContainSingle(m => m.Name == "save-test");
    }

    [Fact]
    public async Task SaveAsync_WithSampleData_WritesSampleFile()
    {
        // Arrange
        var sampleData = new SampleData();
        sampleData.FlatData["variables.firstName"] = "Test";

        var request = new TemplateSaveRequest("sample-save-test", "<div />", "", sampleData);

        // Act
        await _service.SaveAsync(request);

        // Assert
        var loaded = await _sampleDataService.GetAsync("sample-save-test");
        loaded.Should().NotBeNull();
        loaded!.FlatData.Should().ContainKey("variables.firstName");
        loaded.FlatData["variables.firstName"].Should().Be("Test");
    }

    [Fact]
    public async Task DeleteAsync_RemovesFiles()
    {
        // Arrange
        var request = new TemplateSaveRequest("delete-test", "<div />", "");
        await _service.SaveAsync(request);

        // Act
        await _service.DeleteAsync("delete-test");

        // Assert
        var template = await _fileRepo.GetAsync("delete-test");
        template.Should().BeNull();

        var metadata = await _metadataStore.SearchAsync(query: "delete-test");
        metadata.Should().BeEmpty();
    }

    [Fact]
    public async Task DuplicateAsync_CreatesNewCopy()
    {
        // Arrange
        var request = new TemplateSaveRequest("original", "<div>Original</div>", ".orig { }");
        await _service.SaveAsync(request);

        // Act
        await _service.DuplicateAsync("original", "duplicate");

        // Assert
        var duplicate = await _fileRepo.GetAsync("duplicate");
        duplicate.Should().NotBeNull();
        duplicate!.HtmlContent.Should().Be("<div>Original</div>");
        duplicate.CssContent.Should().Be(".orig { }");

        // Original should still exist
        var original = await _fileRepo.GetAsync("original");
        original.Should().NotBeNull();
    }

    [Fact]
    public async Task RenameAsync_RenamesAllFiles()
    {
        // Arrange
        var request = new TemplateSaveRequest("rename-source", "<div>Rename</div>", ".rename { }");
        await _service.SaveAsync(request);

        // Act
        await _service.RenameAsync(new RenameRequest("rename-source", "rename-target"));

        // Assert
        var oldTemplate = await _fileRepo.GetAsync("rename-source");
        oldTemplate.Should().BeNull();

        var newTemplate = await _fileRepo.GetAsync("rename-target");
        newTemplate.Should().NotBeNull();
        newTemplate!.HtmlContent.Should().Be("<div>Rename</div>");

        // Metadata should be renamed
        var metadata = await _metadataStore.SearchAsync(query: "rename-target");
        metadata.Should().ContainSingle(m => m.Name == "rename-target");
    }

    [Fact]
    public async Task SyncMetadataAsync_SyncsFromFileSystem()
    {
        // Arrange - create files directly (not via service)
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "sync-test-a.html"), "<div />");
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "sync-test-b.html"), "<div />");

        // Act
        await _service.SyncMetadataAsync();

        // Assert
        var results = await _metadataStore.SearchAsync();
        results.Should().Contain(r => r.Name == "sync-test-a");
        results.Should().Contain(r => r.Name == "sync-test-b");
    }
}

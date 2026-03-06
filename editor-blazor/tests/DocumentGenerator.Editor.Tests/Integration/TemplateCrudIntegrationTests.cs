using FluentAssertions;
using DocumentGenerator.Editor.Core.DTOs;
using DocumentGenerator.Editor.Core.Models;
using DocumentGenerator.Editor.Infrastructure.Database;
using DocumentGenerator.Editor.Infrastructure.FileSystem;
using DocumentGenerator.Editor.Infrastructure.Services;

namespace DocumentGenerator.Editor.Tests.Integration;

/// <summary>
/// End-to-end integration tests for the full template lifecycle:
/// Create → Read → Update → Rename → Duplicate → Delete
/// Exercises TemplateService, FileTemplateRepository, SqliteMetadataStore,
/// and SampleDataService together.
/// </summary>
public class TemplateCrudIntegrationTests : IAsyncLifetime
{
    private readonly string _tempDir;
    private readonly string _dbPath;
    private readonly TemplateService _service;
    private readonly FileTemplateRepository _fileRepo;
    private readonly SampleDataService _sampleData;
    private readonly SqliteMetadataStore _metadataStore;

    public TemplateCrudIntegrationTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"editor-integration-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        _dbPath = Path.Combine(Path.GetTempPath(), $"editor-integration-{Guid.NewGuid():N}.db");

        _fileRepo = new FileTemplateRepository(_tempDir);
        _sampleData = new SampleDataService(_tempDir);
        _metadataStore = new SqliteMetadataStore($"Data Source={_dbPath}");
        _service = new TemplateService(_fileRepo, _metadataStore, _sampleData);
    }

    public async Task InitializeAsync()
    {
        await _metadataStore.InitializeAsync();
    }

    public Task DisposeAsync()
    {
        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }
        catch { /* temp dir cleanup is best-effort */ }
        // Don't delete the SQLite DB file - connection pool may still hold it open.
        // Let OS temp cleanup handle it.
        return Task.CompletedTask;
    }

    [Fact]
    public async Task FullLifecycle_CreateReadUpdateDeleteTemplate()
    {
        // ── CREATE ──
        var createRequest = new TemplateSaveRequest(
            "integration-test",
            "<div>Initial</div>",
            ".initial { color: red; }",
            SampleData.DefaultSampleData);

        await _service.SaveAsync(createRequest);

        // Verify creation
        var created = await _service.GetAsync("integration-test");
        created.Should().NotBeNull();
        created!.HtmlContent.Should().Be("<div>Initial</div>");
        created.CssContent.Should().Be(".initial { color: red; }");

        // Verify appears in list
        var list = await _service.ListAsync();
        list.Should().Contain(i => i.Name == "integration-test");

        // Verify sample data saved
        var savedSample = await _sampleData.GetAsync("integration-test");
        savedSample.Should().NotBeNull();
        savedSample!.FlatData.Should().ContainKey("variables.firstName");

        // ── UPDATE ──
        var updateRequest = new TemplateSaveRequest(
            "integration-test",
            "<div>Updated</div>",
            ".updated { color: blue; }",
            SampleData.DefaultSampleData);

        await _service.SaveAsync(updateRequest);

        var updated = await _service.GetAsync("integration-test");
        updated.Should().NotBeNull();
        updated!.HtmlContent.Should().Be("<div>Updated</div>");
        updated.CssContent.Should().Be(".updated { color: blue; }");

        // ── DELETE ──
        await _service.DeleteAsync("integration-test");

        var deleted = await _service.GetAsync("integration-test");
        deleted.Should().BeNull();

        var deletedSample = await _sampleData.GetAsync("integration-test");
        deletedSample.Should().BeNull();
    }

    [Fact]
    public async Task RenameTemplate_PreservesContentAndSampleData()
    {
        // Create with sample data
        var sampleDataObj = new SampleData();
        sampleDataObj.FlatData["variables.firstName"] = "IntegrationTest";
        sampleDataObj.FlatData["branding.primaryColour"] = "#FF0000";

        var request = new TemplateSaveRequest(
            "rename-source",
            "<div>Rename Me</div>",
            ".rename { }",
            sampleDataObj);

        await _service.SaveAsync(request);

        // Rename
        await _service.RenameAsync(new RenameRequest("rename-source", "rename-target"));

        // Old name should be gone
        var old = await _service.GetAsync("rename-source");
        old.Should().BeNull();
        var oldSample = await _sampleData.GetAsync("rename-source");
        oldSample.Should().BeNull();

        // New name should have content
        var renamed = await _service.GetAsync("rename-target");
        renamed.Should().NotBeNull();
        renamed!.HtmlContent.Should().Be("<div>Rename Me</div>");

        // New name should have sample data
        var renamedSample = await _sampleData.GetAsync("rename-target");
        renamedSample.Should().NotBeNull();
        renamedSample!.FlatData["variables.firstName"].Should().Be("IntegrationTest");
        renamedSample.FlatData["branding.primaryColour"].Should().Be("#FF0000");
    }

    [Fact]
    public async Task DuplicateTemplate_CopiesContentAndSampleData()
    {
        // Create source
        var sampleDataObj = new SampleData();
        sampleDataObj.FlatData["variables.firstName"] = "Original";

        var request = new TemplateSaveRequest(
            "dup-source",
            "<div>Original</div>",
            ".original { }",
            sampleDataObj);

        await _service.SaveAsync(request);

        // Duplicate
        await _service.DuplicateAsync("dup-source", "dup-target");

        // Source should still exist
        var source = await _service.GetAsync("dup-source");
        source.Should().NotBeNull();
        source!.HtmlContent.Should().Be("<div>Original</div>");

        // Target should be identical content
        var target = await _service.GetAsync("dup-target");
        target.Should().NotBeNull();
        target!.HtmlContent.Should().Be("<div>Original</div>");
        target.CssContent.Should().Be(".original { }");

        // Target should have sample data copy
        var targetSample = await _sampleData.GetAsync("dup-target");
        targetSample.Should().NotBeNull();
        targetSample!.FlatData["variables.firstName"].Should().Be("Original");
    }

    [Fact]
    public async Task DuplicateNonExistentTemplate_Throws()
    {
        var act = async () => await _service.DuplicateAsync("nonexistent", "copy");
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task SyncMetadata_PicksUpFilesCreatedDirectly()
    {
        // Create files directly on disk
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "direct-badge-pulse-a6.html"), "<div/>");
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "direct-badge-pulse-a6.css"), "");

        // Sync
        await _service.SyncMetadataAsync();

        // Should appear in list
        var list = await _service.ListAsync();
        list.Should().Contain(i => i.Name == "direct-badge-pulse-a6");
    }

    [Fact]
    public async Task ListAsync_WithFamilyFilter_ReturnsCorrectSubset()
    {
        // Create templates of different families
        await _service.SaveAsync(new TemplateSaveRequest("badge-pulse-test", "<div/>", ""));
        await _service.SaveAsync(new TemplateSaveRequest("badge-carbon-test", "<div/>", ""));
        await _service.SaveAsync(new TemplateSaveRequest("invoice-test", "<div/>", ""));

        // Filter by family
        var pulseResults = await _service.ListAsync(family: TemplateFamily.Pulse);
        pulseResults.Should().Contain(i => i.Name == "badge-pulse-test");
        pulseResults.Should().NotContain(i => i.Name == "badge-carbon-test");

        var invoiceResults = await _service.ListAsync(family: TemplateFamily.Invoice);
        invoiceResults.Should().Contain(i => i.Name == "invoice-test");
    }

    [Fact]
    public async Task ListAsync_WithSearchQuery_ReturnsMatchingTemplates()
    {
        await _service.SaveAsync(new TemplateSaveRequest("searchable-alpha", "<div/>", ""));
        await _service.SaveAsync(new TemplateSaveRequest("searchable-beta", "<div/>", ""));
        await _service.SaveAsync(new TemplateSaveRequest("other-template", "<div/>", ""));

        var results = await _service.ListAsync(query: "searchable");
        results.Should().HaveCountGreaterThanOrEqualTo(2);
        results.Should().Contain(i => i.Name == "searchable-alpha");
        results.Should().Contain(i => i.Name == "searchable-beta");
    }

    [Fact]
    public async Task SaveAsync_MultipleTimes_OverwritesPreviousContent()
    {
        await _service.SaveAsync(new TemplateSaveRequest("overwrite-test", "<v1/>", ".v1{}"));
        await _service.SaveAsync(new TemplateSaveRequest("overwrite-test", "<v2/>", ".v2{}"));
        await _service.SaveAsync(new TemplateSaveRequest("overwrite-test", "<v3/>", ".v3{}"));

        var final = await _service.GetAsync("overwrite-test");
        final.Should().NotBeNull();
        final!.HtmlContent.Should().Be("<v3/>");
        final.CssContent.Should().Be(".v3{}");
    }
}

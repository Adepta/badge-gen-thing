using System.Text.Json;
using FluentAssertions;
using DocumentGenerator.Editor.Core.DTOs;
using DocumentGenerator.Editor.Core.Models;
using DocumentGenerator.Editor.Infrastructure.Database;
using DocumentGenerator.Editor.Infrastructure.FileSystem;
using DocumentGenerator.Editor.Infrastructure.Services;

namespace DocumentGenerator.Editor.Tests.Integration;

/// <summary>
/// Integration tests verifying sample data round-trips through the full stack:
/// JSON → SampleData → File → SampleData → Handlebars-ready nested dict.
/// </summary>
public class SampleDataLifecycleTests : IAsyncLifetime
{
    private readonly string _tempDir;
    private readonly string _dbPath;
    private readonly TemplateService _templateService;
    private readonly SampleDataService _sampleDataService;
    private readonly SqliteMetadataStore _metadataStore;

    public SampleDataLifecycleTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"editor-sample-int-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        _dbPath = Path.Combine(Path.GetTempPath(), $"editor-sample-int-{Guid.NewGuid():N}.db");

        var fileRepo = new FileTemplateRepository(_tempDir);
        _sampleDataService = new SampleDataService(_tempDir);
        _metadataStore = new SqliteMetadataStore($"Data Source={_dbPath}");
        _templateService = new TemplateService(fileRepo, _metadataStore, _sampleDataService);
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
        return Task.CompletedTask;
    }

    [Fact]
    public async Task SampleData_SaveAndReload_PreservesAllFields()
    {
        var original = new SampleData();
        original.FlatData["branding.companyName"] = "Integration Corp";
        original.FlatData["branding.primaryColour"] = "#FF5733";
        original.FlatData["branding.secondaryColour"] = "#C70039";
        original.FlatData["variables.firstName"] = "Test";
        original.FlatData["variables.lastName"] = "User";
        original.FlatData["variables.attendeeId"] = "INT-001";

        await _sampleDataService.SaveAsync("sample-lifecycle-test", original);

        var loaded = await _sampleDataService.GetAsync("sample-lifecycle-test");

        loaded.Should().NotBeNull();
        loaded!.FlatData.Should().HaveCount(6);
        loaded.FlatData["branding.companyName"].Should().Be("Integration Corp");
        loaded.FlatData["branding.primaryColour"].Should().Be("#FF5733");
        loaded.FlatData["variables.firstName"].Should().Be("Test");
        loaded.FlatData["variables.attendeeId"].Should().Be("INT-001");
    }

    [Fact]
    public async Task SampleData_SavedWithTemplate_ReloadedCorrectly()
    {
        var sampleData = new SampleData();
        sampleData.FlatData["variables.firstName"] = "Integrated";
        sampleData.FlatData["branding.primaryColour"] = "#123456";

        var request = new TemplateSaveRequest(
            "sample-with-template",
            "<div>{{variables.firstName}}</div>",
            ".test { color: {{branding.primaryColour}}; }",
            sampleData);

        await _templateService.SaveAsync(request);

        // Reload sample data
        var loaded = await _sampleDataService.GetAsync("sample-with-template");
        loaded.Should().NotBeNull();

        // Convert to nested for Handlebars
        var nested = loaded!.ToNested();
        nested.Should().ContainKey("variables");
        nested.Should().ContainKey("branding");

        var variables = (Dictionary<string, object>)nested["variables"];
        variables["firstName"].Should().Be("Integrated");

        var branding = (Dictionary<string, object>)nested["branding"];
        branding["primaryColour"].Should().Be("#123456");
    }

    [Fact]
    public async Task SampleData_FromJson_RoundTripsCorrectly()
    {
        // Simulate JSON that might come from an import
        var json = """
        {
            "branding": {
                "companyName": "JSON Corp",
                "primaryColour": "#AABBCC"
            },
            "variables": {
                "firstName": "Json",
                "lastName": "User",
                "nested": {
                    "deep": "value"
                }
            }
        }
        """;

        var element = JsonDocument.Parse(json).RootElement;
        var sampleData = SampleData.FromJsonElement(element);

        // Save to file
        await _sampleDataService.SaveAsync("json-roundtrip", sampleData);

        // Reload
        var loaded = await _sampleDataService.GetAsync("json-roundtrip");
        loaded.Should().NotBeNull();

        // Verify all keys preserved
        loaded!.FlatData["branding.companyName"].Should().Be("JSON Corp");
        loaded.FlatData["branding.primaryColour"].Should().Be("#AABBCC");
        loaded.FlatData["variables.firstName"].Should().Be("Json");
        loaded.FlatData["variables.lastName"].Should().Be("User");
        loaded.FlatData["variables.nested.deep"].Should().Be("value");

        // Convert back to nested for Handlebars
        var nested = loaded.ToNested();
        var variables = (Dictionary<string, object>)nested["variables"];
        var nestedObj = (Dictionary<string, object>)variables["nested"];
        nestedObj["deep"].Should().Be("value");
    }

    [Fact]
    public async Task SampleData_Delete_RemovesFile()
    {
        var data = new SampleData();
        data.FlatData["key"] = "value";

        await _sampleDataService.SaveAsync("delete-sample-test", data);
        var exists = await _sampleDataService.GetAsync("delete-sample-test");
        exists.Should().NotBeNull();

        await _sampleDataService.DeleteAsync("delete-sample-test");

        var deleted = await _sampleDataService.GetAsync("delete-sample-test");
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task SampleData_TemplateRename_PreservesSampleData()
    {
        var sampleData = new SampleData();
        sampleData.FlatData["key"] = "preserve-me";

        var request = new TemplateSaveRequest("rename-sample-src", "<div/>", "", sampleData);
        await _templateService.SaveAsync(request);

        await _templateService.RenameAsync(new RenameRequest("rename-sample-src", "rename-sample-dst"));

        // Old sample data should be gone
        var oldData = await _sampleDataService.GetAsync("rename-sample-src");
        oldData.Should().BeNull();

        // New name should have the data
        var newData = await _sampleDataService.GetAsync("rename-sample-dst");
        newData.Should().NotBeNull();
        newData!.FlatData["key"].Should().Be("preserve-me");
    }
}

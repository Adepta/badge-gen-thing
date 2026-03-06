using FluentAssertions;
using DocumentGenerator.Editor.Core.Models;
using DocumentGenerator.Editor.Infrastructure.Services;

namespace DocumentGenerator.Editor.Tests.Services;

public class SampleDataServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly SampleDataService _service;

    public SampleDataServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"editor-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _service = new SampleDataService(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task GetAsync_ExistingFile_ReturnsSampleData()
    {
        // Arrange - write a sample JSON file directly
        var json = """
        {
            "variables": {
                "firstName": "Jane",
                "lastName": "Smith"
            },
            "branding": {
                "primaryColour": "#6C3CE1"
            }
        }
        """;
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "sample-test-template.json"), json);

        // Act
        var result = await _service.GetAsync("test-template");

        // Assert
        result.Should().NotBeNull();
        result!.FlatData.Should().ContainKey("variables.firstName");
        result.FlatData["variables.firstName"].Should().Be("Jane");
        result.FlatData.Should().ContainKey("variables.lastName");
        result.FlatData["variables.lastName"].Should().Be("Smith");
        result.FlatData.Should().ContainKey("branding.primaryColour");
        result.FlatData["branding.primaryColour"].Should().Be("#6C3CE1");
    }

    [Fact]
    public async Task GetAsync_NonExistent_ReturnsNull()
    {
        var result = await _service.GetAsync("nonexistent-template");
        result.Should().BeNull();
    }

    [Fact]
    public async Task SaveAsync_WritesJsonFile()
    {
        // Arrange
        var data = new SampleData();
        data.FlatData["variables.firstName"] = "John";
        data.FlatData["branding.primaryColour"] = "#FF0000";

        // Act
        await _service.SaveAsync("save-test", data);

        // Assert
        var filePath = Path.Combine(_tempDir, "sample-save-test.json");
        File.Exists(filePath).Should().BeTrue();

        var json = await File.ReadAllTextAsync(filePath);
        json.Should().Contain("firstName");
        json.Should().Contain("John");
        json.Should().Contain("primaryColour");
        json.Should().Contain("#FF0000");
    }

    [Fact]
    public async Task DeleteAsync_RemovesFile()
    {
        // Arrange
        var filePath = Path.Combine(_tempDir, "sample-delete-test.json");
        await File.WriteAllTextAsync(filePath, "{}");

        // Act
        await _service.DeleteAsync("delete-test");

        // Assert
        File.Exists(filePath).Should().BeFalse();
    }

    [Fact]
    public async Task RoundTrip_FlatToNestedAndBack()
    {
        // Arrange
        var original = new SampleData();
        original.FlatData["variables.firstName"] = "Jane";
        original.FlatData["variables.lastName"] = "Smith";
        original.FlatData["branding.primaryColour"] = "#6C3CE1";
        original.FlatData["branding.custom.accentColour"] = "#FF5A5F";

        // Act - save and re-read (goes through flat -> nested JSON -> flat)
        await _service.SaveAsync("roundtrip-test", original);
        var loaded = await _service.GetAsync("roundtrip-test");

        // Assert
        loaded.Should().NotBeNull();
        loaded!.FlatData.Should().HaveCount(original.FlatData.Count);

        foreach (var kvp in original.FlatData)
        {
            loaded.FlatData.Should().ContainKey(kvp.Key);
            loaded.FlatData[kvp.Key].Should().Be(kvp.Value);
        }
    }
}

using FluentAssertions;
using DocumentGenerator.Editor.Core.Models;
using DocumentGenerator.Editor.Infrastructure.FileSystem;
using DocumentGenerator.Editor.Infrastructure.Services;

namespace DocumentGenerator.Editor.Tests.Integration;

/// <summary>
/// Integration tests for the asset upload/list/get/delete lifecycle.
/// </summary>
public class AssetLifecycleIntegrationTests : IDisposable
{
    private readonly string _tempDir;
    private readonly AssetService _service;

    public AssetLifecycleIntegrationTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"editor-asset-int-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        var repo = new FileAssetRepository(_tempDir);
        _service = new AssetService(repo);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task FullLifecycle_UploadListGetDelete()
    {
        // Upload
        var content = new MemoryStream(new byte[] { 0x89, 0x50, 0x4E, 0x47 }); // PNG header bytes
        var asset = await _service.UploadAsync("logo.png", content, "image/png");

        asset.Should().NotBeNull();
        asset.Filename.Should().Be("logo.png");
        asset.ContentType.Should().Be("image/png");
        asset.Size.Should().BeGreaterThanOrEqualTo(0);

        // List
        var list = await _service.ListAsync();
        list.Should().ContainSingle(a => a.Filename == "logo.png");

        // Get
        var stream = await _service.GetAsync("logo.png");
        stream.Should().NotBeNull();
        stream!.Length.Should().Be(4);
        await stream.DisposeAsync();

        // Delete
        await _service.DeleteAsync("logo.png");
        var afterDelete = await _service.ListAsync();
        afterDelete.Should().NotContain(a => a.Filename == "logo.png");
    }

    [Fact]
    public async Task BatchUpload_MultipleFiles_AllStored()
    {
        var files = new List<(string Filename, Stream Content, string ContentType)>
        {
            ("img1.png", new MemoryStream(new byte[] { 1, 2, 3 }), "image/png"),
            ("img2.jpg", new MemoryStream(new byte[] { 4, 5, 6 }), "image/jpeg"),
            ("img3.svg", new MemoryStream(new byte[] { 7, 8, 9 }), "image/svg+xml"),
        };

        var results = await _service.UploadBatchAsync(files);

        results.Should().HaveCount(3);
        results.Should().Contain(a => a.Filename == "img1.png");
        results.Should().Contain(a => a.Filename == "img2.jpg");
        results.Should().Contain(a => a.Filename == "img3.svg");

        var list = await _service.ListAsync();
        list.Should().HaveCount(3);
    }

    [Fact]
    public async Task BatchUpload_ExceedsLimit_Throws()
    {
        var files = Enumerable.Range(1, 11)
            .Select(i => ($"file{i}.png", (Stream)new MemoryStream(new byte[] { 1 }), "image/png"))
            .ToList();

        var act = async () => await _service.UploadBatchAsync(files);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Cannot upload more than*");
    }

    [Fact]
    public async Task Upload_InvalidExtension_Throws()
    {
        var content = new MemoryStream(new byte[] { 1, 2, 3 });
        var act = async () => await _service.UploadAsync("script.js", content, "application/javascript");
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*not allowed*");
    }

    [Fact]
    public async Task Upload_OversizedFile_Throws()
    {
        // 6 MB file exceeds 5 MB limit
        var content = new MemoryStream(new byte[6 * 1024 * 1024]);
        var act = async () => await _service.UploadAsync("huge.png", content, "image/png");
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*exceeds maximum*");
    }

    [Fact]
    public async Task Get_NonExistent_ReturnsNull()
    {
        var stream = await _service.GetAsync("nonexistent.png");
        stream.Should().BeNull();
    }

    [Fact]
    public async Task Upload_MultipleWithSameName_Overwrites()
    {
        var content1 = new MemoryStream(new byte[] { 1, 2, 3 });
        await _service.UploadAsync("same.png", content1, "image/png");

        var content2 = new MemoryStream(new byte[] { 4, 5, 6, 7 });
        await _service.UploadAsync("same.png", content2, "image/png");

        var list = await _service.ListAsync();
        list.Should().ContainSingle(a => a.Filename == "same.png");
    }
}

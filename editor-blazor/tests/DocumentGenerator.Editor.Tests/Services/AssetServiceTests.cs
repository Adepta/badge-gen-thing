using FluentAssertions;
using DocumentGenerator.Editor.Infrastructure.FileSystem;
using DocumentGenerator.Editor.Infrastructure.Services;

namespace DocumentGenerator.Editor.Tests.Services;

public class AssetServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly FileAssetRepository _assetRepo;
    private readonly AssetService _service;

    public AssetServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"editor-assets-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _assetRepo = new FileAssetRepository(_tempDir);
        _service = new AssetService(_assetRepo);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void ValidateFile_ValidImage_Passes()
    {
        // Valid extensions should not throw
        var act = () => _service.UploadAsync("logo.png", new MemoryStream(new byte[] { 0x89, 0x50, 0x4E, 0x47 }), "image/png");
        act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ValidateFile_InvalidExtension_Fails()
    {
        var act = () => _service.UploadAsync("malware.exe", new MemoryStream(new byte[] { 0x4D, 0x5A }), "application/octet-stream");
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*not allowed*");
    }

    [Fact]
    public async Task ValidateFile_TooLarge_Fails()
    {
        // Create a stream that exceeds 5MB
        var largeData = new byte[6 * 1024 * 1024]; // 6MB
        var stream = new MemoryStream(largeData);

        var act = () => _service.UploadAsync("large-image.png", stream, "image/png");
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*exceeds maximum*");
    }

    [Fact]
    public async Task UploadAsync_StoresFile()
    {
        // Arrange
        var content = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }; // PNG header
        var stream = new MemoryStream(content);

        // Act
        var asset = await _service.UploadAsync("test-image.png", stream, "image/png");

        // Assert
        asset.Should().NotBeNull();
        asset.Filename.Should().Be("test-image.png");
        asset.Url.Should().Contain("test-image.png");

        // Verify file actually exists on disk with correct content
        var filePath = Path.Combine(_tempDir, "test-image.png");
        File.Exists(filePath).Should().BeTrue();
        var savedContent = await File.ReadAllBytesAsync(filePath);
        savedContent.Should().BeEquivalentTo(content);
    }

    [Fact]
    public async Task ListAsync_ReturnsAssets()
    {
        // Arrange - upload two images
        await _service.UploadAsync("img1.png", new MemoryStream(new byte[] { 0x89, 0x50 }), "image/png");
        await _service.UploadAsync("img2.jpg", new MemoryStream(new byte[] { 0xFF, 0xD8 }), "image/jpeg");

        // Act
        var assets = await _service.ListAsync();

        // Assert
        assets.Should().HaveCount(2);
        assets.Should().Contain(a => a.Filename == "img1.png");
        assets.Should().Contain(a => a.Filename == "img2.jpg");
    }

    [Fact]
    public async Task DeleteAsync_RemovesFile()
    {
        // Arrange
        await _service.UploadAsync("to-delete.png", new MemoryStream(new byte[] { 0x89, 0x50 }), "image/png");
        File.Exists(Path.Combine(_tempDir, "to-delete.png")).Should().BeTrue();

        // Act
        await _service.DeleteAsync("to-delete.png");

        // Assert
        File.Exists(Path.Combine(_tempDir, "to-delete.png")).Should().BeFalse();
    }

    [Theory]
    [InlineData(".png")]
    [InlineData(".jpg")]
    [InlineData(".jpeg")]
    [InlineData(".gif")]
    [InlineData(".svg")]
    [InlineData(".webp")]
    public async Task UploadAsync_AcceptsAllValidExtensions(string extension)
    {
        var filename = $"test{extension}";
        var stream = new MemoryStream(new byte[] { 0x00, 0x01 });

        var asset = await _service.UploadAsync(filename, stream, $"image/{extension.TrimStart('.')}");
        asset.Filename.Should().Be(filename);
    }

    [Theory]
    [InlineData(".exe")]
    [InlineData(".js")]
    [InlineData(".html")]
    [InlineData(".pdf")]
    [InlineData(".zip")]
    public async Task UploadAsync_RejectsInvalidExtensions(string extension)
    {
        var filename = $"test{extension}";
        var stream = new MemoryStream(new byte[] { 0x00, 0x01 });

        var act = () => _service.UploadAsync(filename, stream, "application/octet-stream");
        await act.Should().ThrowAsync<ArgumentException>();
    }
}

using DocumentGenerator.Editor.Core.Interfaces;
using DocumentGenerator.Editor.Core.Models;

namespace DocumentGenerator.Editor.Infrastructure.Services;

/// <summary>
/// Validation wrapper around <see cref="IAssetRepository"/>.
/// Enforces file type, file size, and upload count restrictions.
/// </summary>
public class AssetService
{
    private readonly IAssetRepository _assetRepo;

    /// <summary>Allowed image file extensions.</summary>
    public static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".svg", ".webp"
    };

    /// <summary>Maximum file size in bytes (5 MB).</summary>
    public const long MaxFileSize = 5 * 1024 * 1024;

    /// <summary>Maximum number of files per single upload batch.</summary>
    public const int MaxFilesPerUpload = 10;

    /// <summary>
    /// Creates a new asset service wrapping the given repository.
    /// </summary>
    /// <param name="assetRepo">The underlying asset repository.</param>
    public AssetService(IAssetRepository assetRepo)
    {
        _assetRepo = assetRepo ?? throw new ArgumentNullException(nameof(assetRepo));
    }

    /// <summary>
    /// Lists all uploaded assets.
    /// </summary>
    public async Task<IReadOnlyList<Asset>> ListAsync()
    {
        return await _assetRepo.ListAsync();
    }

    /// <summary>
    /// Uploads a single asset after validating file type and size.
    /// </summary>
    /// <param name="filename">Original file name.</param>
    /// <param name="content">File content stream.</param>
    /// <param name="contentType">MIME content type.</param>
    /// <returns>An <see cref="Asset"/> describing the stored file.</returns>
    /// <exception cref="ArgumentException">If the file type is not allowed.</exception>
    public async Task<Asset> UploadAsync(string filename, Stream content, string contentType)
    {
        ValidateFileType(filename);
        ValidateFileSize(content);
        return await _assetRepo.UploadAsync(filename, content, contentType);
    }

    /// <summary>
    /// Uploads multiple assets in a batch.
    /// </summary>
    /// <param name="files">Collection of (filename, stream, contentType) tuples.</param>
    /// <returns>List of stored asset records.</returns>
    /// <exception cref="InvalidOperationException">If the batch exceeds <see cref="MaxFilesPerUpload"/>.</exception>
    public async Task<IReadOnlyList<Asset>> UploadBatchAsync(IReadOnlyList<(string Filename, Stream Content, string ContentType)> files)
    {
        ArgumentNullException.ThrowIfNull(files);

        if (files.Count > MaxFilesPerUpload)
            throw new InvalidOperationException($"Cannot upload more than {MaxFilesPerUpload} files at once.");

        var results = new List<Asset>(files.Count);
        foreach (var (filename, content, contentType) in files)
        {
            var asset = await UploadAsync(filename, content, contentType);
            results.Add(asset);
        }

        return results;
    }

    /// <summary>
    /// Deletes an asset by filename.
    /// </summary>
    /// <param name="filename">The file name to delete.</param>
    public async Task DeleteAsync(string filename)
    {
        await _assetRepo.DeleteAsync(filename);
    }

    /// <summary>
    /// Retrieves an asset's content stream.
    /// </summary>
    /// <param name="filename">The file name.</param>
    /// <returns>A readable stream, or <c>null</c> if not found.</returns>
    public async Task<Stream?> GetAsync(string filename)
    {
        return await _assetRepo.GetAsync(filename);
    }

    /// <summary>
    /// Validates that the file extension is in the allowed set.
    /// </summary>
    private static void ValidateFileType(string filename)
    {
        var ext = Path.GetExtension(filename);
        if (string.IsNullOrEmpty(ext) || !AllowedExtensions.Contains(ext))
            throw new ArgumentException($"File type '{ext}' is not allowed. Allowed types: {string.Join(", ", AllowedExtensions)}", nameof(filename));
    }

    /// <summary>
    /// Validates that the stream does not exceed the maximum file size.
    /// </summary>
    private static void ValidateFileSize(Stream content)
    {
        if (content.CanSeek && content.Length > MaxFileSize)
            throw new InvalidOperationException($"File size ({content.Length} bytes) exceeds maximum allowed size ({MaxFileSize} bytes).");
    }
}

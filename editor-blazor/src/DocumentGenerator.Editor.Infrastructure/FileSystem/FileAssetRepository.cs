using System.Text.RegularExpressions;
using DocumentGenerator.Editor.Core.Interfaces;
using DocumentGenerator.Editor.Core.Models;

namespace DocumentGenerator.Editor.Infrastructure.FileSystem;

/// <summary>
/// File-system implementation of <see cref="IAssetRepository"/>.
/// Stores uploaded image assets in a configured directory.
/// </summary>
public partial class FileAssetRepository : IAssetRepository
{
    private readonly string _assetsDir;

    /// <summary>Maximum allowed file size: 5 MB.</summary>
    public const long MaxFileSize = 5 * 1024 * 1024;

    /// <summary>Allowed image file extensions.</summary>
    public static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".svg", ".webp"
    };

    private static readonly Dictionary<string, string> ExtensionToContentType = new(StringComparer.OrdinalIgnoreCase)
    {
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".gif"] = "image/gif",
        [".svg"] = "image/svg+xml",
        [".webp"] = "image/webp"
    };

    /// <summary>
    /// Creates a new repository rooted at the given directory.
    /// </summary>
    /// <param name="assetsDir">Absolute path to the assets directory.</param>
    public FileAssetRepository(string assetsDir)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetsDir);
        _assetsDir = Path.GetFullPath(assetsDir);
        Directory.CreateDirectory(_assetsDir);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<Asset>> ListAsync()
    {
        var dir = new DirectoryInfo(_assetsDir);
        if (!dir.Exists)
            return Task.FromResult<IReadOnlyList<Asset>>(Array.Empty<Asset>());

        var files = dir.GetFiles()
            .Where(f => AllowedExtensions.Contains(f.Extension))
            .Select(f => new Asset(
                f.Name,
                f.Length,
                $"assets/{f.Name}",
                ExtensionToContentType.GetValueOrDefault(f.Extension, "application/octet-stream"),
                f.LastWriteTimeUtc))
            .ToList();

        return Task.FromResult<IReadOnlyList<Asset>>(files);
    }

    /// <inheritdoc />
    public async Task<Asset> UploadAsync(string filename, Stream content, string contentType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filename);
        ArgumentNullException.ThrowIfNull(content);

        var sanitised = SanitiseFilename(filename);
        var ext = Path.GetExtension(sanitised);

        if (!AllowedExtensions.Contains(ext))
            throw new ArgumentException($"File type '{ext}' is not allowed. Allowed types: {string.Join(", ", AllowedExtensions)}");

        var safePath = SafePath(sanitised);

        await using var fileStream = new FileStream(safePath, FileMode.Create, FileAccess.Write, FileShare.None);

        // Read in chunks to enforce size limit
        var buffer = new byte[8192];
        long totalRead = 0;
        int bytesRead;

        while ((bytesRead = await content.ReadAsync(buffer)) > 0)
        {
            totalRead += bytesRead;
            if (totalRead > MaxFileSize)
            {
                // Clean up the partial file
                fileStream.Close();
                File.Delete(safePath);
                throw new InvalidOperationException($"File exceeds maximum size of {MaxFileSize / (1024 * 1024)} MB.");
            }

            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
        }

        var fileInfo = new FileInfo(safePath);
        return new Asset(
            sanitised,
            fileInfo.Length,
            $"assets/{sanitised}",
            contentType,
            fileInfo.LastWriteTimeUtc);
    }

    /// <inheritdoc />
    public Task DeleteAsync(string filename)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filename);

        var safePath = SafePath(filename);
        if (File.Exists(safePath))
            File.Delete(safePath);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<Stream?> GetAsync(string filename)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filename);

        var safePath = SafePath(filename);
        if (!File.Exists(safePath))
            return Task.FromResult<Stream?>(null);

        Stream stream = new FileStream(safePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult<Stream?>(stream);
    }

    /// <summary>
    /// Resolves a filename to an absolute path within the assets directory,
    /// preventing path traversal attacks.
    /// </summary>
    private string SafePath(string filename)
    {
        var full = Path.GetFullPath(Path.Combine(_assetsDir, filename));
        if (!full.StartsWith(_assetsDir, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Path traversal detected: '{filename}' resolves outside the assets directory.");
        return full;
    }

    /// <summary>
    /// Sanitises a filename by removing path separators and replacing invalid characters.
    /// </summary>
    private static string SanitiseFilename(string filename)
    {
        // Strip any directory components
        var name = Path.GetFileName(filename);

        // Replace invalid filename characters with underscores
        name = InvalidCharsPattern().Replace(name, "_");

        // Ensure the name is not empty
        if (string.IsNullOrWhiteSpace(Path.GetFileNameWithoutExtension(name)))
            name = $"asset_{DateTime.UtcNow:yyyyMMddHHmmss}{Path.GetExtension(name)}";

        return name;
    }

    [GeneratedRegex(@"[^\w\-\.]")]
    private static partial Regex InvalidCharsPattern();
}

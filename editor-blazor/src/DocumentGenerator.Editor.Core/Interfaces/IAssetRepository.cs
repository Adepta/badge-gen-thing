using DocumentGenerator.Editor.Core.Models;

namespace DocumentGenerator.Editor.Core.Interfaces;

/// <summary>
/// Provides storage and retrieval for uploaded image assets.
/// </summary>
public interface IAssetRepository
{
    /// <summary>
    /// Lists all assets currently stored on disk.
    /// </summary>
    Task<IReadOnlyList<Asset>> ListAsync();

    /// <summary>
    /// Uploads an asset to disk.
    /// </summary>
    /// <param name="filename">The desired file name.</param>
    /// <param name="content">The file content stream.</param>
    /// <param name="contentType">MIME content type.</param>
    /// <returns>An <see cref="Asset"/> record describing the stored file.</returns>
    Task<Asset> UploadAsync(string filename, Stream content, string contentType);

    /// <summary>
    /// Deletes an asset from disk.
    /// </summary>
    /// <param name="filename">The file name to delete.</param>
    Task DeleteAsync(string filename);

    /// <summary>
    /// Retrieves an asset's content stream.
    /// </summary>
    /// <param name="filename">The file name.</param>
    /// <returns>A readable stream, or <c>null</c> if the file does not exist.</returns>
    Task<Stream?> GetAsync(string filename);
}

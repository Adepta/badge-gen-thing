namespace DocumentGenerator.Editor.Core.Models;

/// <summary>
/// Represents an uploaded asset (image) stored on disk.
/// </summary>
/// <param name="Filename">The sanitised file name.</param>
/// <param name="Size">File size in bytes.</param>
/// <param name="Url">Relative URL used to reference the asset in templates.</param>
/// <param name="ContentType">MIME content type (e.g. "image/png").</param>
/// <param name="UploadedAt">UTC timestamp when the asset was uploaded / last modified.</param>
public record Asset(
    string Filename,
    long Size,
    string Url,
    string ContentType,
    DateTime UploadedAt);

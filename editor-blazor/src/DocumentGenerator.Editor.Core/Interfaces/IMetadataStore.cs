using DocumentGenerator.Editor.Core.DTOs;
using DocumentGenerator.Editor.Core.Models;

namespace DocumentGenerator.Editor.Core.Interfaces;

/// <summary>
/// SQLite-backed metadata index for templates. Provides fast search and filtering
/// without scanning the file system on every request.
/// </summary>
public interface IMetadataStore
{
    /// <summary>
    /// Initialises the database schema (runs migrations).
    /// </summary>
    Task InitializeAsync();

    /// <summary>
    /// Inserts or updates metadata for a template.
    /// </summary>
    /// <param name="name">Template name.</param>
    /// <param name="family">Detected template family.</param>
    /// <param name="size">Detected size preset.</param>
    /// <param name="modifiedAt">Last-modified timestamp.</param>
    Task UpsertTemplateAsync(string name, TemplateFamily family, SizePreset size, DateTime modifiedAt);

    /// <summary>
    /// Removes a template from the metadata index.
    /// </summary>
    /// <param name="name">Template name.</param>
    Task DeleteTemplateAsync(string name);

    /// <summary>
    /// Searches templates with optional text and family filters.
    /// </summary>
    /// <param name="query">Optional text to match against the template name (LIKE search).</param>
    /// <param name="family">Optional family filter.</param>
    /// <returns>Matching template list items.</returns>
    Task<IReadOnlyList<TemplateListItem>> SearchAsync(string? query = null, TemplateFamily? family = null);

    /// <summary>
    /// Renames a template in the metadata index.
    /// </summary>
    /// <param name="oldName">Current name.</param>
    /// <param name="newName">New name.</param>
    Task RenameTemplateAsync(string oldName, string newName);
}

using DocumentGenerator.Editor.Core.DTOs;
using DocumentGenerator.Editor.Core.Models;

namespace DocumentGenerator.Editor.Core.Interfaces;

/// <summary>
/// Provides file-system–backed CRUD operations for template files (.html / .css).
/// </summary>
public interface ITemplateRepository
{
    /// <summary>
    /// Lists all templates found on disk.
    /// </summary>
    Task<IReadOnlyList<TemplateListItem>> ListAsync();

    /// <summary>
    /// Loads the full content of a template by name.
    /// </summary>
    /// <param name="name">Template name (without extension).</param>
    /// <returns>The template, or <c>null</c> if not found.</returns>
    Task<Template?> GetAsync(string name);

    /// <summary>
    /// Writes HTML and CSS content to disk for the given template name.
    /// </summary>
    /// <param name="name">Template name (without extension).</param>
    /// <param name="htmlContent">The HTML / Handlebars content.</param>
    /// <param name="cssContent">The CSS content.</param>
    Task SaveAsync(string name, string htmlContent, string cssContent);

    /// <summary>
    /// Deletes a template and all associated files (.html, .css, sample JSON).
    /// </summary>
    /// <param name="name">Template name (without extension).</param>
    Task DeleteAsync(string name);

    /// <summary>
    /// Renames a template and all associated files atomically.
    /// </summary>
    /// <param name="oldName">Current template name.</param>
    /// <param name="newName">Desired new template name.</param>
    Task RenameAsync(string oldName, string newName);

    /// <summary>
    /// Checks whether a template exists on disk.
    /// </summary>
    /// <param name="name">Template name (without extension).</param>
    Task<bool> ExistsAsync(string name);
}

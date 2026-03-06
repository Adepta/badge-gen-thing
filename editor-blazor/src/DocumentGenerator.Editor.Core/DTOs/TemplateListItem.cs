using DocumentGenerator.Editor.Core.Models;

namespace DocumentGenerator.Editor.Core.DTOs;

/// <summary>
/// Lightweight DTO for displaying templates in the sidebar card grid.
/// </summary>
/// <param name="Name">Template name (file stem).</param>
/// <param name="Family">Detected design family.</param>
/// <param name="SizePreset">Detected paper/card size.</param>
/// <param name="LastModified">Last-modified timestamp, or <c>null</c> if unknown.</param>
/// <param name="HasCss">Whether a companion .css file exists.</param>
public record TemplateListItem(
    string Name,
    TemplateFamily Family,
    SizePreset SizePreset,
    DateTime? LastModified,
    bool HasCss);

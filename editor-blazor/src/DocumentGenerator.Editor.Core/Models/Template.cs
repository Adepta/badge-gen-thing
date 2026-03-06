namespace DocumentGenerator.Editor.Core.Models;

/// <summary>
/// Represents a full template with its HTML and CSS content.
/// </summary>
public class Template
{
    /// <summary>
    /// Template name (file stem, e.g. "badge-pulse-a6"). Used as the unique identifier.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// HTML content of the template (Handlebars markup).
    /// </summary>
    public string HtmlContent { get; set; } = string.Empty;

    /// <summary>
    /// CSS content associated with the template.
    /// </summary>
    public string CssContent { get; set; } = string.Empty;

    /// <summary>
    /// The visual design family this template belongs to.
    /// </summary>
    public TemplateFamily Family { get; set; } = TemplateFamily.Custom;

    /// <summary>
    /// The paper / card size preset for this template.
    /// </summary>
    public SizePreset SizePreset { get; set; } = SizePreset.A6;

    /// <summary>
    /// When the template was first created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the template was last modified on disk.
    /// </summary>
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;
}

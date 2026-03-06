namespace DocumentGenerator.Editor.Core.Models;

/// <summary>
/// Identifies the visual design family a template belongs to.
/// </summary>
public enum TemplateFamily
{
    /// <summary>Modern, vibrant badge design.</summary>
    Pulse,

    /// <summary>Professional, clean badge design.</summary>
    Executive,

    /// <summary>Dark, bold badge design.</summary>
    Carbon,

    /// <summary>Invoice / billing document.</summary>
    Invoice,

    /// <summary>User-created or unrecognised family.</summary>
    Custom
}

/// <summary>
/// Extension methods for <see cref="TemplateFamily"/>.
/// </summary>
public static class TemplateFamilyExtensions
{
    /// <summary>
    /// Parses the template family from a template name.
    /// </summary>
    /// <param name="templateName">The template file name (without extension).</param>
    /// <returns>The detected <see cref="TemplateFamily"/>.</returns>
    /// <example>
    /// "badge-pulse-a6" → Pulse, "badge-executive-cc" → Executive, "invoice" → Invoice
    /// </example>
    public static TemplateFamily FromTemplateName(string templateName)
    {
        ArgumentNullException.ThrowIfNull(templateName);

        var lower = templateName.ToLowerInvariant();

        if (lower.Contains("pulse")) return TemplateFamily.Pulse;
        if (lower.Contains("executive")) return TemplateFamily.Executive;
        if (lower.Contains("carbon")) return TemplateFamily.Carbon;
        if (lower.Contains("invoice")) return TemplateFamily.Invoice;

        return TemplateFamily.Custom;
    }
}

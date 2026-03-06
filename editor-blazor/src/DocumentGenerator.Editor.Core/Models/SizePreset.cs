namespace DocumentGenerator.Editor.Core.Models;

/// <summary>
/// Physical dimensions for a printed document.
/// </summary>
/// <param name="WidthMm">Width in millimetres.</param>
/// <param name="HeightMm">Height in millimetres.</param>
public record Dimensions(double WidthMm, double HeightMm)
{
    /// <inheritdoc />
    public override string ToString() => $"{WidthMm}×{HeightMm}mm";
}

/// <summary>
/// Standard paper / card size presets used for template preview.
/// </summary>
public enum SizePreset
{
    /// <summary>A6 – 105 × 148 mm (default badge size).</summary>
    A6,

    /// <summary>Credit-card – 85.6 × 54 mm.</summary>
    CreditCard,

    /// <summary>A4 – 210 × 297 mm (invoices, letters).</summary>
    A4,

    /// <summary>Custom / user-defined dimensions.</summary>
    Custom
}

/// <summary>
/// Extension and helper methods for <see cref="SizePreset"/>.
/// </summary>
public static class SizePresetExtensions
{
    /// <summary>
    /// Returns the physical dimensions for a known preset.
    /// </summary>
    /// <param name="preset">The size preset.</param>
    /// <returns>A <see cref="Dimensions"/> record, or <c>null</c> for <see cref="SizePreset.Custom"/>.</returns>
    public static Dimensions? GetDimensions(this SizePreset preset) => preset switch
    {
        SizePreset.A6 => new Dimensions(105, 148),
        SizePreset.CreditCard => new Dimensions(85.6, 54),
        SizePreset.A4 => new Dimensions(210, 297),
        _ => null
    };

    /// <summary>
    /// Detects the size preset from a template name by inspecting the suffix.
    /// </summary>
    /// <param name="templateName">The template file name (without extension).</param>
    /// <returns>The detected <see cref="SizePreset"/>.</returns>
    /// <example>
    /// "badge-pulse-a6" → A6, "badge-carbon-cc" → CreditCard, "invoice" → A4
    /// </example>
    public static SizePreset FromTemplateName(string templateName)
    {
        ArgumentNullException.ThrowIfNull(templateName);

        var lower = templateName.ToLowerInvariant();

        if (lower.EndsWith("-a6")) return SizePreset.A6;
        if (lower.EndsWith("-cc")) return SizePreset.CreditCard;
        if (lower.EndsWith("-a4")) return SizePreset.A4;
        if (lower.Contains("invoice")) return SizePreset.A4;

        return SizePreset.Custom;
    }
}

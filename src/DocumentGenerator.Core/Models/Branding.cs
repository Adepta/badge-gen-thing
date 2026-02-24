namespace DocumentGenerator.Core.Models;

/// <summary>
/// Branding configuration injected into every document render.
/// Colours, fonts, and logo are available as Handlebars variables
/// under the {{branding.*}} namespace.
/// </summary>
public sealed class Branding
{
    /// <summary>Friendly name shown in headers/footers, e.g. "Acme Corp".</summary>
    public string CompanyName { get; init; } = string.Empty;

    /// <summary>URL or base-64 data URI for the company logo.</summary>
    public string? LogoUrl { get; init; }

    /// <summary>Primary brand colour as a CSS value, e.g. "#1A73E8".</summary>
    public string? PrimaryColour { get; init; }

    /// <summary>Secondary brand colour as a CSS value.</summary>
    public string? SecondaryColour { get; init; }

    /// <summary>CSS font-family string for headings.</summary>
    public string? HeadingFont { get; init; }

    /// <summary>CSS font-family string for body text.</summary>
    public string? BodyFont { get; init; }

    /// <summary>
    /// Any additional branding key/value pairs that templates may reference
    /// via {{branding.custom.key}}.
    /// </summary>
    public Dictionary<string, string> Custom { get; init; } = [];
}

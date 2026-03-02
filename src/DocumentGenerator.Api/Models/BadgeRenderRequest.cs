using System.ComponentModel.DataAnnotations;

namespace DocumentGenerator.Api.Models;

/// <summary>
/// Request body for POST /api/badges/render.
/// The iPad (via the bridge) sends attendee data and template selection here.
/// </summary>
public sealed record BadgeRenderRequest
{
    /// <summary>
    /// The template to use, e.g. "badge-pulse-a6", "badge-executive-cc".
    /// Must correspond to an HTML/CSS template pair in the templates directory.
    /// </summary>
    [Required]
    public string TemplateName { get; init; } = string.Empty;

    /// <summary>
    /// Attendee / badge data injected into the Handlebars template as {{variables.*}}.
    /// Common keys: firstName, lastName, jobTitle, company, ticketType, attendeeId.
    /// </summary>
    [Required]
    public Dictionary<string, object?> Variables { get; init; } = [];

    /// <summary>
    /// Optional branding overrides. When null, the template's default branding is used.
    /// </summary>
    public BadgeBrandingRequest? Branding { get; init; }

    /// <summary>
    /// Output format. "Pdf" (default) or "Png".
    /// </summary>
    public string Format { get; init; } = "Pdf";

    /// <summary>
    /// Caller-supplied correlation ID — echoed back in the response so the bridge
    /// can match async replies. When null, the API generates one.
    /// </summary>
    public Guid? CorrelationId { get; init; }
}

/// <summary>
/// Optional per-request branding overrides that supersede the template's default
/// branding values when present. All properties are nullable; <see langword="null"/>
/// means "use template default".
/// </summary>
public sealed record BadgeBrandingRequest
{
    /// <summary>Display name of the organising company shown on the badge.</summary>
    public string? CompanyName { get; init; }

    /// <summary>Absolute URL of the company logo image to embed in the badge.</summary>
    public string? LogoUrl { get; init; }

    /// <summary>Primary brand colour as a CSS value, e.g. <c>"#7B2CBF"</c>.</summary>
    public string? PrimaryColour { get; init; }

    /// <summary>Secondary / accent brand colour as a CSS value.</summary>
    public string? SecondaryColour { get; init; }

    /// <summary>Google Fonts or system font name for headings, e.g. <c>"Poppins"</c>.</summary>
    public string? HeadingFont { get; init; }

    /// <summary>Google Fonts or system font name for body text, e.g. <c>"Inter"</c>.</summary>
    public string? BodyFont { get; init; }

    /// <summary>
    /// Arbitrary key-value pairs injected into the template under <c>{{branding.custom.*}}</c>.
    /// Useful for template-specific overrides not covered by the standard properties.
    /// </summary>
    public Dictionary<string, string> Custom { get; init; } = [];
}

using System.ComponentModel.DataAnnotations;

namespace DocumentGenerator.Bridge.Models;

/// <summary>
/// Request body accepted by the bridge <c>POST /print</c> and <c>POST /render</c> endpoints.
/// Sent by the iPad over the local network.
/// </summary>
public sealed record PrintRequest
{
    /// <summary>
    /// Badge template to render, e.g. <c>"badge-pulse-a6"</c>.
    /// Must match a template available on the cloud Badge Producer.
    /// </summary>
    [Required]
    public string TemplateName { get; init; } = string.Empty;

    /// <summary>
    /// Attendee / badge data — injected into the Handlebars template as <c>{{variables.*}}</c>.
    /// Typical keys: <c>firstName</c>, <c>lastName</c>, <c>jobTitle</c>, <c>company</c>,
    /// <c>ticketType</c>, <c>attendeeId</c>.
    /// </summary>
    [Required]
    public Dictionary<string, object?> Variables { get; init; } = [];

    /// <summary>
    /// Optional branding overrides forwarded verbatim to the cloud API.
    /// When null, the template's default branding applies.
    /// </summary>
    public BrandingRequest? Branding { get; init; }

    /// <summary>
    /// Name of the local printer to use.
    /// When null or empty, the bridge's configured default printer is used.
    /// Only relevant for <c>POST /print</c>; ignored by <c>POST /render</c>.
    /// </summary>
    public string? PrinterName { get; init; }

    /// <summary>
    /// Caller-supplied correlation ID echoed back in the response.
    /// Useful for the iPad to match async responses.
    /// When null, the bridge generates one.
    /// </summary>
    public Guid? CorrelationId { get; init; }

    /// <summary>
    /// Output format: <c>"Pdf"</c> (default) or <c>"Png"</c>.
    /// Forwarded to the cloud API render request.
    /// </summary>
    public string? Format { get; init; }
}

/// <summary>
/// Optional branding overrides sent from the iPad.
/// </summary>
public sealed record BrandingRequest
{
    /// <summary>Company name displayed on the badge.</summary>
    public string? CompanyName { get; init; }
    /// <summary>URL or Base64 data URI for the company logo.</summary>
    public string? LogoUrl { get; init; }
    /// <summary>Primary brand colour as a CSS value, e.g. <c>"#1A73E8"</c>.</summary>
    public string? PrimaryColour { get; init; }
    /// <summary>Secondary brand colour as a CSS value.</summary>
    public string? SecondaryColour { get; init; }
    /// <summary>CSS font-family string for headings.</summary>
    public string? HeadingFont { get; init; }
    /// <summary>CSS font-family string for body text.</summary>
    public string? BodyFont { get; init; }
    /// <summary>Additional freeform branding key-value pairs.</summary>
    public Dictionary<string, string> Custom { get; init; } = [];
}

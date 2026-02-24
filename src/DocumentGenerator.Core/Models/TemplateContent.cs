namespace DocumentGenerator.Core.Models;

/// <summary>
/// The raw HTML and optional CSS that form the document body.
/// Both fields are Handlebars templates — {{variables}} will be substituted
/// before the content is handed to Chromium.
/// </summary>
public sealed class TemplateContent
{
    /// <summary>
    /// Full HTML document or fragment. May reference {{branding.*}} and
    /// any key from the Variables dictionary.
    /// </summary>
    public string Html { get; init; } = string.Empty;

    /// <summary>
    /// Optional CSS injected into a &lt;style&gt; block inside the rendered page.
    /// Also Handlebars-processed, so brand colours can be used as variables.
    /// </summary>
    public string? Css { get; init; }

    /// <summary>
    /// Optional Handlebars partial definitions keyed by partial name.
    /// Registered before rendering so templates can use {{> partialName}}.
    /// </summary>
    public Dictionary<string, string> Partials { get; init; } = [];
}

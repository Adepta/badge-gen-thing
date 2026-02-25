namespace DocumentGenerator.Core.Models;

/// <summary>
/// The raw HTML and optional CSS that form the document body.
/// Both fields are Handlebars templates — {{variables}} will be substituted
/// before the content is handed to Chromium.
///
/// Content may be supplied inline (<see cref="Html"/>/<see cref="Css"/>) or
/// by referencing external files (<see cref="HtmlPath"/>/<see cref="CssPath"/>).
/// When both are present, inline content takes precedence.
/// </summary>
public sealed class TemplateContent
{
    /// <summary>
    /// Full HTML document or fragment. May reference {{branding.*}} and
    /// any key from the Variables dictionary.
    /// Ignored when <see cref="HtmlPath"/> is provided and non-empty.
    /// </summary>
    public string Html { get; init; } = string.Empty;

    /// <summary>
    /// Optional CSS injected into a &lt;style&gt; block inside the rendered page.
    /// Also Handlebars-processed, so brand colours can be used as variables.
    /// Ignored when <see cref="CssPath"/> is provided and non-empty.
    /// </summary>
    public string? Css { get; init; }

    /// <summary>
    /// Path to an external <c>.html</c> file containing the Handlebars template.
    /// May be absolute or relative to the directory of the parent JSON template file.
    /// When set, its content is read at load time and used in place of <see cref="Html"/>.
    /// </summary>
    public string? HtmlPath { get; init; }

    /// <summary>
    /// Path to an external <c>.css</c> file containing the stylesheet.
    /// May be absolute or relative to the directory of the parent JSON template file.
    /// When set, its content is read at load time and used in place of <see cref="Css"/>.
    /// </summary>
    public string? CssPath { get; init; }

    /// <summary>
    /// Optional Handlebars partial definitions keyed by partial name.
    /// Registered before rendering so templates can use {{> partialName}}.
    /// </summary>
    public Dictionary<string, string> Partials { get; init; } = [];
}

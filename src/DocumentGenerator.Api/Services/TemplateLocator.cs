using DocumentGenerator.Core.Models;

namespace DocumentGenerator.Api.Services;

/// <summary>
/// Resolves a named badge template (e.g. <c>"badge-pulse-a6"</c>) to a fully populated
/// <see cref="DocumentTemplate"/> by reading the corresponding HTML and CSS files from
/// the configured templates directory.
/// </summary>
/// <remarks>
/// Templates are stored as pairs of files:
/// <list type="bullet">
///   <item><c>{name}.html</c> — Handlebars HTML template</item>
///   <item><c>{name}.css</c> — companion stylesheet (optional)</item>
/// </list>
/// The <see cref="DocumentTemplate"/> returned uses <see cref="TemplateContent.HtmlPath"/>
/// and <see cref="TemplateContent.CssPath"/> so the existing
/// <c>FileTemplateContentResolver</c> resolves the files at render time.
/// </remarks>
public sealed class TemplateLocator
{
    private readonly string _templatesPath;
    private readonly ILogger<TemplateLocator> _logger;

    /// <summary>
    /// Initialises a new <see cref="TemplateLocator"/> using the configured templates directory.
    /// </summary>
    /// <param name="configuration">Application configuration (reads <c>DocumentGenerator:TemplatesPath</c>).</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    public TemplateLocator(IConfiguration configuration, ILogger<TemplateLocator> logger)
    {
        var configured = configuration["DocumentGenerator:TemplatesPath"] ?? "templates";

        // Resolve relative paths against the binary output directory so templates
        // copied by MSBuild (CopyToOutputDirectory) are found regardless of the
        // current working directory when the process is launched.
        _templatesPath = Path.IsPathRooted(configured)
            ? configured
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, configured));

        _logger = logger;
    }

    /// <summary>
    /// Builds a <see cref="DocumentTemplate"/> for the given template name and variable bag.
    /// </summary>
    /// <param name="templateName">
    /// Short name of the template, e.g. <c>"badge-pulse-a6"</c>.
    /// Must correspond to <c>{templateName}.html</c> in the templates directory.
    /// </param>
    /// <param name="variables">Attendee / badge data injected as Handlebars variables.</param>
    /// <param name="branding">Optional branding overrides. When null, sensible defaults are used.</param>
    /// <returns>A <see cref="DocumentTemplate"/> ready to pass to <c>IDocumentPipeline</c>.</returns>
    /// <exception cref="FileNotFoundException">
    /// Thrown when the HTML template file does not exist on disk.
    /// </exception>
    public DocumentTemplate Resolve(
        string templateName,
        Dictionary<string, object?> variables,
        Branding? branding = null)
    {
        var htmlPath = Path.Combine(_templatesPath, $"{templateName}.html");
        var cssPath  = Path.Combine(_templatesPath, $"{templateName}.css");

        if (!File.Exists(htmlPath))
        {
            _logger.LogWarning("Template not found: {HtmlPath}", htmlPath);
            throw new FileNotFoundException($"Badge template '{templateName}' not found.", htmlPath);
        }

        var hasCss = File.Exists(cssPath);

        _logger.LogDebug("Resolved template {TemplateName} — html={HtmlPath} css={CssPath}",
            templateName, htmlPath, hasCss ? cssPath : "(none)");

        return new DocumentTemplate
        {
            DocumentType = "badge",
            Version      = "1.0",
            Branding     = branding ?? new Branding(),
            Template = new TemplateContent
            {
                HtmlPath = htmlPath,
                CssPath  = hasCss ? cssPath : null,
                Partials = []
            },
            Variables = variables,
            Pdf = ResolvePdfOptions(templateName)
        };
    }

    /// <summary>
    /// Lists all available template names by scanning the templates directory for <c>.html</c> files.
    /// </summary>
    /// <returns>A sequence of template name strings (without extension).</returns>
    public IEnumerable<string> ListTemplates()
    {
        if (!Directory.Exists(_templatesPath))
            return [];

        return Directory
            .EnumerateFiles(_templatesPath, "*.html")
            .Select(f => Path.GetFileNameWithoutExtension(f))
            .Where(n => !n.StartsWith("sample-", StringComparison.OrdinalIgnoreCase))
            .OrderBy(n => n);
    }

    // Derives sensible PdfOptions based on the template name suffix.
    private static PdfOptions ResolvePdfOptions(string templateName) =>
        templateName switch
        {
            var n when n.EndsWith("-cc",  StringComparison.OrdinalIgnoreCase) =>
                new PdfOptions { Width = "85.6mm", Height = "54mm",  PrintBackground = true, Margins = ZeroMargins() },

            var n when n.EndsWith("-a6",  StringComparison.OrdinalIgnoreCase) =>
                new PdfOptions { Width = "105mm",  Height = "148mm", PrintBackground = true, Margins = ZeroMargins() },

            _ =>
                new PdfOptions { Format = "A4", PrintBackground = true }
        };

    private static PdfMargins ZeroMargins() =>
        new() { Top = "0mm", Bottom = "0mm", Left = "0mm", Right = "0mm" };
}

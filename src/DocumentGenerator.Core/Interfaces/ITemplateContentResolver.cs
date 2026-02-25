using DocumentGenerator.Core.Models;

namespace DocumentGenerator.Core.Interfaces;

/// <summary>
/// Resolves a <see cref="DocumentTemplate"/> whose <see cref="TemplateContent"/>
/// may reference external HTML and CSS files, returning a new template with the
/// file contents loaded inline so the rendering pipeline only ever sees strings.
/// </summary>
public interface ITemplateContentResolver
{
    /// <summary>
    /// Reads any external HTML/CSS files referenced by
    /// <see cref="TemplateContent.HtmlPath"/> and <see cref="TemplateContent.CssPath"/>
    /// and returns a new <see cref="DocumentTemplate"/> with those values inlined.
    /// </summary>
    /// <param name="template">The template to resolve.</param>
    /// <param name="basePath">
    /// The directory used to resolve relative paths.
    /// Typically the directory that contains the JSON template file.
    /// </param>
    /// <param name="cancellationToken">Token to cancel I/O operations.</param>
    /// <returns>
    /// A <see cref="DocumentTemplate"/> whose <see cref="TemplateContent.Html"/>
    /// and <see cref="TemplateContent.Css"/> are fully populated strings.
    /// </returns>
    Task<DocumentTemplate> ResolveAsync(
        DocumentTemplate template,
        string basePath,
        CancellationToken cancellationToken = default);
}

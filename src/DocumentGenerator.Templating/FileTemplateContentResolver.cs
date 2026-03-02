using DocumentGenerator.Core.Errors;
using DocumentGenerator.Core.Interfaces;
using DocumentGenerator.Core.Models;
using Microsoft.Extensions.Logging;

namespace DocumentGenerator.Templating;

/// <summary>
/// Resolves external HTML and CSS file references inside a <see cref="DocumentTemplate"/>.
///
/// When <see cref="TemplateContent.HtmlPath"/> or <see cref="TemplateContent.CssPath"/>
/// are set, the files are read from disk and their contents replace
/// <see cref="TemplateContent.Html"/> and <see cref="TemplateContent.Css"/> respectively.
/// Relative paths are resolved against the <c>basePath</c> supplied at call time
/// (normally the directory that contains the JSON template file).
/// Inline content is left untouched if the corresponding path property is absent.
/// </summary>
public sealed class FileTemplateContentResolver(ILogger<FileTemplateContentResolver> logger) : ITemplateContentResolver
{
    private readonly ILogger<FileTemplateContentResolver> _logger = logger;

    /// <inheritdoc />
    public async Task<DocumentTemplate> ResolveAsync(
        DocumentTemplate template,
        string basePath,
        CancellationToken cancellationToken = default)
    {
        var content = template.Template;
        var htmlChanged = false;
        var cssChanged  = false;

        var resolvedHtml = content.Html;
        var resolvedCss  = content.Css;

        if (!string.IsNullOrWhiteSpace(content.HtmlPath))
        {
            var fullPath = ResolvePath(content.HtmlPath, basePath);
            _logger.LogDebug("Loading HTML from {Path}", fullPath);
            try
            {
                resolvedHtml = await File.ReadAllTextAsync(fullPath, cancellationToken).ConfigureAwait(false);
            }
            catch (FileNotFoundException ex)
            {
                throw TemplateException.ReadFailed(template.DocumentType, fullPath, ex);
            }
            catch (IOException ex)
            {
                throw TemplateException.ReadFailed(template.DocumentType, fullPath, ex);
            }
            htmlChanged = true;
        }

        if (!string.IsNullOrWhiteSpace(content.CssPath))
        {
            var fullPath = ResolvePath(content.CssPath, basePath);
            _logger.LogDebug("Loading CSS from {Path}", fullPath);
            try
            {
                resolvedCss = await File.ReadAllTextAsync(fullPath, cancellationToken).ConfigureAwait(false);
            }
            catch (FileNotFoundException ex)
            {
                throw TemplateException.ReadFailed(template.DocumentType, fullPath, ex);
            }
            catch (IOException ex)
            {
                throw TemplateException.ReadFailed(template.DocumentType, fullPath, ex);
            }
            cssChanged = true;
        }

        if (!htmlChanged && !cssChanged)
            return template;

        // Return a new template with the resolved inline content; keep everything else identical
        var resolvedContent = new TemplateContent
        {
            Html     = resolvedHtml,
            Css      = resolvedCss,
            HtmlPath = content.HtmlPath,
            CssPath  = content.CssPath,
            Partials = content.Partials
        };

        return new DocumentTemplate
        {
            DocumentType = template.DocumentType,
            Version      = template.Version,
            Branding     = template.Branding,
            Template     = resolvedContent,
            Variables    = template.Variables,
            Pdf          = template.Pdf
        };
    }

    private static string ResolvePath(string path, string basePath)
    {
        if (Path.IsPathRooted(path))
            return path;

        return Path.GetFullPath(Path.Combine(basePath, path));
    }
}

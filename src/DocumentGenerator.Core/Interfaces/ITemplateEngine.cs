using DocumentGenerator.Core.Models;

namespace DocumentGenerator.Core.Interfaces;

/// <summary>
/// Resolves a <see cref="DocumentTemplate"/> into fully-rendered HTML
/// by processing Handlebars expressions against branding + variables.
/// </summary>
public interface ITemplateEngine
{
    /// <summary>
    /// Renders the template's HTML (and optional CSS) using the supplied
    /// branding and variable data, returning a complete HTML string ready
    /// for Chromium to load.
    /// </summary>
    /// <param name="template">The document template containing HTML, CSS, branding and variables.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A complete HTML string with CSS injected, ready to pass to <see cref="IDocumentRenderer"/>.</returns>
    /// <exception cref="OperationCanceledException">Thrown if <paramref name="cancellationToken"/> is cancelled.</exception>
    Task<string> RenderAsync(DocumentTemplate template, CancellationToken cancellationToken = default);
}

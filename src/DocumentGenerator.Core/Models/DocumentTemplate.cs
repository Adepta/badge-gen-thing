namespace DocumentGenerator.Core.Models;

/// <summary>
/// The top-level template definition loaded from a JSON file or queue payload.
/// </summary>
public sealed class DocumentTemplate
{
    /// <summary>
    /// Logical document type identifier, e.g. "invoice", "report", "certificate".
    /// Used for routing, logging, and future template selection.
    /// </summary>
    public string DocumentType { get; init; } = string.Empty;

    /// <summary>Version tag for the template schema, e.g. "1.0".</summary>
    public string Version { get; init; } = "1.0";

    /// <summary>Branding applied to this document.</summary>
    public Branding Branding { get; init; } = new();

    /// <summary>The HTML/CSS template body.</summary>
    public TemplateContent Template { get; init; } = new();

    /// <summary>
    /// Freeform variable bag injected into the Handlebars context at render time.
    /// Supports nested objects — anything JSON-serialisable.
    /// </summary>
    public Dictionary<string, object?> Variables { get; init; } = [];

    /// <summary>Optional PDF output settings.</summary>
    public PdfOptions Pdf { get; init; } = new();
}

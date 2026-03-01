namespace DocumentGenerator.Core.Errors;

/// <summary>
/// Thrown when a template cannot be located, read, compiled, or rendered.
/// </summary>
public sealed class TemplateException : DocumentGeneratorException
{
    /// <summary>The template name that triggered the failure, if known.</summary>
    public string? TemplateName { get; }

    private TemplateException(
        ErrorCode code,
        string message,
        string? templateName,
        Exception? inner = null)
        : base(
            code,
            message,
            BuildContext(templateName),
            inner!)
    {
        TemplateName = templateName;
    }

    // ── Factory methods ───────────────────────────────────────────────────────

    /// <summary>
    /// Creates a <see cref="ErrorCode.TemplateNotFound"/> exception.
    /// </summary>
    public static TemplateException NotFound(string templateName, string resolvedPath) =>
        new(ErrorCode.TemplateNotFound,
            $"Template '{templateName}' not found at '{resolvedPath}'.",
            templateName);

    /// <summary>
    /// Creates a <see cref="ErrorCode.TemplateNameInvalid"/> exception.
    /// </summary>
    public static TemplateException InvalidName(string templateName) =>
        new(ErrorCode.TemplateNameInvalid,
            $"Template name '{templateName}' is null, empty, or contains invalid characters.",
            templateName);

    /// <summary>
    /// Creates a <see cref="ErrorCode.TemplateReadFailed"/> exception.
    /// </summary>
    public static TemplateException ReadFailed(string templateName, string path, Exception inner) =>
        new(ErrorCode.TemplateReadFailed,
            $"Failed to read template '{templateName}' from '{path}'.",
            templateName, inner);

    /// <summary>
    /// Creates a <see cref="ErrorCode.TemplateCompileFailed"/> exception.
    /// </summary>
    public static TemplateException CompileFailed(string templateName, Exception inner) =>
        new(ErrorCode.TemplateCompileFailed,
            $"Handlebars compilation failed for template '{templateName}'.",
            templateName, inner);

    /// <summary>
    /// Creates a <see cref="ErrorCode.TemplateRenderFailed"/> exception.
    /// </summary>
    public static TemplateException RenderFailed(string templateName, Exception inner) =>
        new(ErrorCode.TemplateRenderFailed,
            $"Handlebars rendering failed for template '{templateName}'.",
            templateName, inner);

    // ─────────────────────────────────────────────────────────────────────────

    private static IReadOnlyDictionary<string, object?> BuildContext(string? templateName) =>
        new Dictionary<string, object?> { ["templateName"] = templateName };
}

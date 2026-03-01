namespace DocumentGenerator.Console;

/// <summary>
/// Top-level configuration for the file-based document generator worker.
/// Bind from <c>appsettings.json</c> under the <c>"DocumentGenerator"</c> key.
/// </summary>
public sealed class DocumentGeneratorOptions
{
    /// <summary>Configuration section key used when binding from <c>appsettings.json</c>.</summary>
    public const string SectionName = "DocumentGenerator";

    /// <summary>
    /// Path to the directory containing <c>.json</c> template descriptor files.
    /// Relative paths are resolved against the current working directory at startup.
    /// Defaults to <c>"templates"</c>.
    /// </summary>
    public string TemplatesPath { get; init; } = "templates";

    /// <summary>
    /// Path to the directory where rendered PDF files are written.
    /// Created automatically if it does not exist.
    /// Defaults to <c>"output"</c>.
    /// </summary>
    public string OutputPath    { get; init; } = "output";
}

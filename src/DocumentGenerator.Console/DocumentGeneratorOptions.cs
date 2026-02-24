namespace DocumentGenerator.Console;

/// <summary>
/// Top-level configuration for the file-based document generator worker.
/// Bind from <c>appsettings.json</c> under the <c>"DocumentGenerator"</c> key.
/// </summary>
public sealed class DocumentGeneratorOptions
{
    public const string SectionName = "DocumentGenerator";

    public string TemplatesPath { get; init; } = "templates";
    public string OutputPath    { get; init; } = "output";
}

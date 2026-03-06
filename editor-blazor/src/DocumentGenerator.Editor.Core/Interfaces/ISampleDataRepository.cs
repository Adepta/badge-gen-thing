using DocumentGenerator.Editor.Core.Models;

namespace DocumentGenerator.Editor.Core.Interfaces;

/// <summary>
/// Provides storage for per-template sample / test data JSON files.
/// </summary>
public interface ISampleDataRepository
{
    /// <summary>
    /// Loads sample data for a template.
    /// </summary>
    /// <param name="templateName">Template name (without extension).</param>
    /// <returns>The sample data, or <c>null</c> if no file exists.</returns>
    Task<SampleData?> GetAsync(string templateName);

    /// <summary>
    /// Saves sample data for a template, overwriting any existing file.
    /// </summary>
    /// <param name="templateName">Template name (without extension).</param>
    /// <param name="data">The sample data to persist.</param>
    Task SaveAsync(string templateName, SampleData data);

    /// <summary>
    /// Deletes the sample data file for a template.
    /// </summary>
    /// <param name="templateName">Template name (without extension).</param>
    Task DeleteAsync(string templateName);
}

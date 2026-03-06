using System.Text.Json;
using DocumentGenerator.Editor.Core.Interfaces;
using DocumentGenerator.Editor.Core.Models;

namespace DocumentGenerator.Editor.Infrastructure.Services;

/// <summary>
/// File-system implementation of <see cref="ISampleDataRepository"/>.
/// Manages sample-{templateName}.json files alongside templates.
/// </summary>
public class SampleDataService : ISampleDataRepository
{
    private readonly string _templatesDir;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Creates a new sample data service rooted at the templates directory.
    /// </summary>
    /// <param name="templatesDir">Absolute path to the templates directory.</param>
    public SampleDataService(string templatesDir)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templatesDir);
        _templatesDir = Path.GetFullPath(templatesDir);
    }

    /// <inheritdoc />
    public async Task<SampleData?> GetAsync(string templateName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templateName);

        var filePath = GetSamplePath(templateName);
        if (!File.Exists(filePath))
            return null;

        var json = await File.ReadAllTextAsync(filePath);

        try
        {
            using var doc = JsonDocument.Parse(json);
            return SampleData.FromJsonElement(doc.RootElement);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async Task SaveAsync(string templateName, SampleData data)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templateName);
        ArgumentNullException.ThrowIfNull(data);

        var filePath = GetSamplePath(templateName);
        var nested = data.ToNested();
        var json = JsonSerializer.Serialize(nested, JsonOptions);
        await File.WriteAllTextAsync(filePath, json);
    }

    /// <inheritdoc />
    public Task DeleteAsync(string templateName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templateName);

        var filePath = GetSamplePath(templateName);
        if (File.Exists(filePath))
            File.Delete(filePath);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Returns the full path for a sample data file.
    /// </summary>
    private string GetSamplePath(string templateName)
    {
        var filePath = Path.GetFullPath(Path.Combine(_templatesDir, $"sample-{templateName}.json"));

        // Path traversal protection
        if (!filePath.StartsWith(_templatesDir, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Path traversal detected for template name '{templateName}'.");

        return filePath;
    }
}

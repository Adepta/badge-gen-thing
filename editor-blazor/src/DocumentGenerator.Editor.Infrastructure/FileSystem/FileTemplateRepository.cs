using System.Text.RegularExpressions;
using DocumentGenerator.Editor.Core.DTOs;
using DocumentGenerator.Editor.Core.Interfaces;
using DocumentGenerator.Editor.Core.Models;

namespace DocumentGenerator.Editor.Infrastructure.FileSystem;

/// <summary>
/// File-system implementation of <see cref="ITemplateRepository"/>.
/// Reads and writes .html / .css template files from a configured directory.
/// </summary>
public partial class FileTemplateRepository : ITemplateRepository
{
    private readonly string _templatesDir;

    /// <summary>
    /// Creates a new repository rooted at the given directory.
    /// </summary>
    /// <param name="templatesDir">Absolute path to the templates directory.</param>
    public FileTemplateRepository(string templatesDir)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templatesDir);
        _templatesDir = Path.GetFullPath(templatesDir);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<TemplateListItem>> ListAsync()
    {
        var dir = new DirectoryInfo(_templatesDir);
        if (!dir.Exists)
            return Task.FromResult<IReadOnlyList<TemplateListItem>>(Array.Empty<TemplateListItem>());

        var htmlFiles = dir.GetFiles("*.html");
        var items = new List<TemplateListItem>(htmlFiles.Length);

        foreach (var html in htmlFiles)
        {
            var name = Path.GetFileNameWithoutExtension(html.Name);
            var cssPath = Path.Combine(_templatesDir, $"{name}.css");
            var hasCss = File.Exists(cssPath);
            var family = TemplateFamilyExtensions.FromTemplateName(name);
            var size = SizePresetExtensions.FromTemplateName(name);

            items.Add(new TemplateListItem(name, family, size, html.LastWriteTimeUtc, hasCss));
        }

        return Task.FromResult<IReadOnlyList<TemplateListItem>>(items);
    }

    /// <inheritdoc />
    public async Task<Template?> GetAsync(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var htmlPath = SafePath($"{name}.html");
        if (!File.Exists(htmlPath))
            return null;

        var htmlContent = await File.ReadAllTextAsync(htmlPath);

        var cssPath = SafePath($"{name}.css");
        var cssContent = File.Exists(cssPath) ? await File.ReadAllTextAsync(cssPath) : string.Empty;

        var fileInfo = new FileInfo(htmlPath);
        return new Template
        {
            Name = name,
            HtmlContent = htmlContent,
            CssContent = cssContent,
            Family = TemplateFamilyExtensions.FromTemplateName(name),
            SizePreset = SizePresetExtensions.FromTemplateName(name),
            CreatedAt = fileInfo.CreationTimeUtc,
            ModifiedAt = fileInfo.LastWriteTimeUtc
        };
    }

    /// <inheritdoc />
    public async Task SaveAsync(string name, string htmlContent, string cssContent)
    {
        ValidateName(name);

        var htmlPath = SafePath($"{name}.html");
        var cssPath = SafePath($"{name}.css");

        await File.WriteAllTextAsync(htmlPath, htmlContent);
        await File.WriteAllTextAsync(cssPath, cssContent);
    }

    /// <inheritdoc />
    public Task DeleteAsync(string name)
    {
        ValidateName(name);

        var htmlPath = SafePath($"{name}.html");
        var cssPath = SafePath($"{name}.css");
        var samplePath = SafePath($"sample-{name}.json");

        if (File.Exists(htmlPath)) File.Delete(htmlPath);
        if (File.Exists(cssPath)) File.Delete(cssPath);
        if (File.Exists(samplePath)) File.Delete(samplePath);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RenameAsync(string oldName, string newName)
    {
        ValidateName(oldName);
        ValidateName(newName);

        RenameIfExists($"{oldName}.html", $"{newName}.html");
        RenameIfExists($"{oldName}.css", $"{newName}.css");
        RenameIfExists($"sample-{oldName}.json", $"sample-{newName}.json");

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<bool> ExistsAsync(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var htmlPath = SafePath($"{name}.html");
        return Task.FromResult(File.Exists(htmlPath));
    }

    /// <summary>
    /// Resolves a relative file name to an absolute path within the templates directory,
    /// and validates the result stays within bounds (path traversal prevention).
    /// </summary>
    /// <param name="relativeName">File name (e.g. "badge-pulse-a6.html").</param>
    /// <returns>The absolute path.</returns>
    /// <exception cref="InvalidOperationException">If the resolved path escapes the templates directory.</exception>
    private string SafePath(string relativeName)
    {
        var full = Path.GetFullPath(Path.Combine(_templatesDir, relativeName));
        if (!full.StartsWith(_templatesDir, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Path traversal detected: '{relativeName}' resolves outside the templates directory.");
        return full;
    }

    /// <summary>
    /// Validates that a template name contains only word characters and hyphens.
    /// </summary>
    private static void ValidateName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (!ValidNamePattern().IsMatch(name))
            throw new ArgumentException($"Template name '{name}' contains invalid characters. Only letters, digits, underscores, and hyphens are allowed.", nameof(name));
    }

    /// <summary>
    /// Renames a file if the source exists.
    /// </summary>
    private void RenameIfExists(string oldRelative, string newRelative)
    {
        var oldPath = SafePath(oldRelative);
        var newPath = SafePath(newRelative);
        if (File.Exists(oldPath))
            File.Move(oldPath, newPath, overwrite: false);
    }

    [GeneratedRegex(@"^[\w\-]+$")]
    private static partial Regex ValidNamePattern();
}

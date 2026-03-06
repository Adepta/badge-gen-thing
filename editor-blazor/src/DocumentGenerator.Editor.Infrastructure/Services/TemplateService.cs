using DocumentGenerator.Editor.Core.DTOs;
using DocumentGenerator.Editor.Core.Interfaces;
using DocumentGenerator.Editor.Core.Models;

namespace DocumentGenerator.Editor.Infrastructure.Services;

/// <summary>
/// Orchestrates template operations across the file repository, metadata store,
/// and sample data repository.
/// </summary>
public class TemplateService
{
    private readonly ITemplateRepository _templateRepo;
    private readonly IMetadataStore _metadataStore;
    private readonly ISampleDataRepository _sampleDataRepo;

    /// <summary>
    /// Creates a new template service.
    /// </summary>
    /// <param name="templateRepo">File-system template repository.</param>
    /// <param name="metadataStore">SQLite metadata store.</param>
    /// <param name="sampleDataRepo">Sample data repository.</param>
    public TemplateService(
        ITemplateRepository templateRepo,
        IMetadataStore metadataStore,
        ISampleDataRepository sampleDataRepo)
    {
        _templateRepo = templateRepo ?? throw new ArgumentNullException(nameof(templateRepo));
        _metadataStore = metadataStore ?? throw new ArgumentNullException(nameof(metadataStore));
        _sampleDataRepo = sampleDataRepo ?? throw new ArgumentNullException(nameof(sampleDataRepo));
    }

    /// <summary>
    /// Lists all templates. Tries the metadata store first, falls back to file system.
    /// </summary>
    /// <param name="query">Optional search text.</param>
    /// <param name="family">Optional family filter.</param>
    public async Task<IReadOnlyList<TemplateListItem>> ListAsync(string? query = null, TemplateFamily? family = null)
    {
        var results = await _metadataStore.SearchAsync(query, family);

        if (results.Count > 0)
            return results;

        // Fall back to file system scan
        return await _templateRepo.ListAsync();
    }

    /// <summary>
    /// Loads a template's full content from disk.
    /// </summary>
    /// <param name="name">Template name.</param>
    public async Task<Template?> GetAsync(string name)
    {
        return await _templateRepo.GetAsync(name);
    }

    /// <summary>
    /// Saves template content to disk and updates metadata. Optionally saves sample data.
    /// </summary>
    /// <param name="request">The save request containing name, HTML, CSS, and optional sample data.</param>
    public async Task SaveAsync(TemplateSaveRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        await _templateRepo.SaveAsync(request.Name, request.HtmlContent, request.CssContent);

        var family = TemplateFamilyExtensions.FromTemplateName(request.Name);
        var size = SizePresetExtensions.FromTemplateName(request.Name);
        await _metadataStore.UpsertTemplateAsync(request.Name, family, size, DateTime.UtcNow);

        if (request.SampleData is not null)
        {
            await _sampleDataRepo.SaveAsync(request.Name, request.SampleData);
        }
    }

    /// <summary>
    /// Deletes a template and all associated files and metadata.
    /// </summary>
    /// <param name="name">Template name.</param>
    public async Task DeleteAsync(string name)
    {
        await _templateRepo.DeleteAsync(name);
        await _metadataStore.DeleteTemplateAsync(name);
        await _sampleDataRepo.DeleteAsync(name);
    }

    /// <summary>
    /// Renames a template across files, metadata, and sample data.
    /// </summary>
    /// <param name="request">The rename request.</param>
    public async Task RenameAsync(RenameRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        await _templateRepo.RenameAsync(request.OldName, request.NewName);
        await _metadataStore.RenameTemplateAsync(request.OldName, request.NewName);

        // Rename sample data file (handled via delete + re-save or file rename)
        var sampleData = await _sampleDataRepo.GetAsync(request.OldName);
        if (sampleData is not null)
        {
            await _sampleDataRepo.SaveAsync(request.NewName, sampleData);
            await _sampleDataRepo.DeleteAsync(request.OldName);
        }
    }

    /// <summary>
    /// Duplicates a template with a new name.
    /// </summary>
    /// <param name="sourceName">Name of the template to copy.</param>
    /// <param name="newName">Name for the duplicate.</param>
    public async Task DuplicateAsync(string sourceName, string newName)
    {
        var source = await _templateRepo.GetAsync(sourceName)
            ?? throw new InvalidOperationException($"Template '{sourceName}' not found.");

        await _templateRepo.SaveAsync(newName, source.HtmlContent, source.CssContent);

        var family = TemplateFamilyExtensions.FromTemplateName(newName);
        var size = SizePresetExtensions.FromTemplateName(newName);
        await _metadataStore.UpsertTemplateAsync(newName, family, size, DateTime.UtcNow);

        // Copy sample data if it exists
        var sampleData = await _sampleDataRepo.GetAsync(sourceName);
        if (sampleData is not null)
        {
            await _sampleDataRepo.SaveAsync(newName, sampleData);
        }
    }

    /// <summary>
    /// Scans the file system and synchronises the metadata store.
    /// Typically called once on application startup.
    /// </summary>
    public async Task SyncMetadataAsync()
    {
        var fileTemplates = await _templateRepo.ListAsync();

        foreach (var item in fileTemplates)
        {
            await _metadataStore.UpsertTemplateAsync(
                item.Name,
                item.Family,
                item.SizePreset,
                item.LastModified ?? DateTime.UtcNow);
        }
    }
}

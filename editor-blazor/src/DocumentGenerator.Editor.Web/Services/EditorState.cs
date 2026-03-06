using DocumentGenerator.Editor.Core.Models;

namespace DocumentGenerator.Editor.Web.Services;

/// <summary>
/// Central state management service - scoped per SignalR circuit.
/// Tracks the currently open template, dirty state, preview mode, and sample data.
/// </summary>
public class EditorState
{
    // Current template
    public string? CurrentTemplateName { get; private set; }
    public string HtmlContent { get; private set; } = string.Empty;
    public string CssContent { get; private set; } = string.Empty;
    public bool IsDirty { get; private set; }
    public bool IsLoading { get; private set; }

    // Preview mode
    public string PreviewMode { get; private set; } = "editor"; // "editor" or "live"

    // Sample data
    public SampleData? SampleData { get; private set; }

    // Save state
    public string SaveStatus { get; private set; } = "Ready";
    public DateTime? LastSavedAt { get; private set; }

    // Cursor position (from active editor)
    public int CursorLine { get; set; } = 1;
    public int CursorColumn { get; set; } = 1;

    // Events
    public event Func<Task>? OnStateChanged;
    public event Func<Task>? OnTemplateLoaded;
    public event Func<Task>? OnContentChanged;
    public event Func<Task>? OnSaveRequested;
    public event Func<Task>? OnPreviewModeChanged;
    public event Func<Task>? OnSampleDataChanged;

    /// <summary>
    /// Loads a template into the editor state, replacing any current content.
    /// </summary>
    public async Task LoadTemplateAsync(string name, string html, string css, SampleData? sampleData)
    {
        CurrentTemplateName = name;
        HtmlContent = html;
        CssContent = css;
        SampleData = sampleData;
        IsDirty = false;
        SaveStatus = "Ready";
        await NotifyTemplateLoaded();
        await NotifyStateChanged();
    }

    /// <summary>
    /// Updates the HTML content and marks the state as dirty.
    /// </summary>
    public async Task UpdateHtmlAsync(string html)
    {
        HtmlContent = html;
        IsDirty = true;
        await NotifyContentChanged();
        await NotifyStateChanged();
    }

    /// <summary>
    /// Updates the CSS content and marks the state as dirty.
    /// </summary>
    public async Task UpdateCssAsync(string css)
    {
        CssContent = css;
        IsDirty = true;
        await NotifyContentChanged();
        await NotifyStateChanged();
    }

    /// <summary>
    /// Marks the current state as saved (not dirty).
    /// </summary>
    public async Task MarkSavedAsync()
    {
        IsDirty = false;
        SaveStatus = "Saved";
        LastSavedAt = DateTime.Now;
        await NotifyStateChanged();
    }

    /// <summary>
    /// Sets the save status text (e.g. "Saving...", "Error", "Saved").
    /// </summary>
    public async Task SetSaveStatusAsync(string status)
    {
        SaveStatus = status;
        await NotifyStateChanged();
    }

    /// <summary>
    /// Sets the preview mode ("editor" or "live").
    /// </summary>
    public async Task SetPreviewModeAsync(string mode)
    {
        PreviewMode = mode;
        await OnPreviewModeChanged.InvokeAllAsync();
        await NotifyStateChanged();
    }

    /// <summary>
    /// Updates the sample data and notifies listeners.
    /// </summary>
    public async Task UpdateSampleDataAsync(SampleData data)
    {
        SampleData = data;
        await OnSampleDataChanged.InvokeAllAsync();
        await NotifyStateChanged();
    }

    /// <summary>
    /// Sets the loading state.
    /// </summary>
    public async Task SetLoadingAsync(bool loading)
    {
        IsLoading = loading;
        await NotifyStateChanged();
    }

    /// <summary>
    /// Requests a save operation. Listeners (e.g. MainLayout) should handle this.
    /// </summary>
    public async Task RequestSaveAsync()
    {
        await OnSaveRequested.InvokeAllAsync();
    }

    /// <summary>
    /// Clears all state (used when closing a template).
    /// </summary>
    public async Task ClearAsync()
    {
        CurrentTemplateName = null;
        HtmlContent = string.Empty;
        CssContent = string.Empty;
        IsDirty = false;
        SampleData = null;
        SaveStatus = "Ready";
        LastSavedAt = null;
        CursorLine = 1;
        CursorColumn = 1;
        await NotifyStateChanged();
    }

    /// <summary>
    /// Clears all state synchronously (for use in non-async contexts).
    /// Note: does not fire state change events.
    /// </summary>
    public void Clear()
    {
        CurrentTemplateName = null;
        HtmlContent = string.Empty;
        CssContent = string.Empty;
        IsDirty = false;
        SampleData = null;
        SaveStatus = "Ready";
        LastSavedAt = null;
        CursorLine = 1;
        CursorColumn = 1;
    }

    private Task NotifyStateChanged() => OnStateChanged.InvokeAllAsync();
    private Task NotifyTemplateLoaded() => OnTemplateLoaded.InvokeAllAsync();
    private Task NotifyContentChanged() => OnContentChanged.InvokeAllAsync();
}

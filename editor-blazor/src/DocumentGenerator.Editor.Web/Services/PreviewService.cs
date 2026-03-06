namespace DocumentGenerator.Editor.Web.Services;

/// <summary>
/// Orchestrates debounced preview rendering. Subscribes to EditorState changes
/// and produces rendered preview HTML via HandlebarsInteropService.
/// </summary>
public class PreviewService : IDisposable
{
    private readonly HandlebarsInteropService _handlebars;
    private readonly EditorState _editorState;

    private Timer? _debounceTimer;
    private readonly int _debounceMs = 280;
    private bool _disposed;

    /// <summary>
    /// The last successfully rendered preview HTML.
    /// </summary>
    public string PreviewHtml { get; private set; } = string.Empty;

    /// <summary>
    /// Whether a render is currently in progress.
    /// </summary>
    public bool IsRendering { get; private set; }

    /// <summary>
    /// Fires when a new preview HTML has been rendered and is ready to display.
    /// </summary>
    public event Func<Task>? OnPreviewUpdated;

    public PreviewService(HandlebarsInteropService handlebars, EditorState editorState)
    {
        _handlebars = handlebars;
        _editorState = editorState;

        // Subscribe to state changes to auto-render
        _editorState.OnContentChanged += HandleContentChanged;
        _editorState.OnSampleDataChanged += HandleSampleDataChanged;
        _editorState.OnPreviewModeChanged += HandlePreviewModeChanged;
        _editorState.OnTemplateLoaded += HandleTemplateLoaded;
    }

    /// <summary>
    /// Schedules a debounced render. Resets the timer on each call.
    /// </summary>
    public void ScheduleRender()
    {
        if (_disposed) return;

        _debounceTimer?.Dispose();
        _debounceTimer = new Timer(
            _ => _ = RenderInternalAsync(),
            null,
            _debounceMs,
            Timeout.Infinite);
    }

    /// <summary>
    /// Triggers an immediate render (bypasses debounce).
    /// </summary>
    public async Task RenderAsync()
    {
        await RenderInternalAsync();
    }

    private async Task RenderInternalAsync()
    {
        if (_disposed) return;
        if (string.IsNullOrEmpty(_editorState.CurrentTemplateName)) return;

        IsRendering = true;

        try
        {
            // Build nested data from sample data for Handlebars
            var data = _editorState.SampleData?.ToNested() ?? new Dictionary<string, object>();
            var mode = _editorState.PreviewMode ?? "editor";

            var html = await _handlebars.BuildPreviewHtmlAsync(
                _editorState.HtmlContent,
                _editorState.CssContent,
                data,
                mode);

            PreviewHtml = html;
        }
        catch (Exception ex)
        {
            // Show error in preview
            PreviewHtml = $@"<!DOCTYPE html><html><body style=""color:red;padding:16px;font-family:monospace;font-size:12px;"">
                <strong>Preview Error:</strong><br/>{System.Net.WebUtility.HtmlEncode(ex.Message)}</body></html>";
        }
        finally
        {
            IsRendering = false;
        }

        await OnPreviewUpdated.InvokeAllAsync();
    }

    private Task HandleContentChanged()
    {
        ScheduleRender();
        return Task.CompletedTask;
    }

    private Task HandleSampleDataChanged()
    {
        ScheduleRender();
        return Task.CompletedTask;
    }

    private Task HandlePreviewModeChanged()
    {
        ScheduleRender();
        return Task.CompletedTask;
    }

    private async Task HandleTemplateLoaded()
    {
        // Render immediately when a new template is loaded
        await RenderInternalAsync();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _debounceTimer?.Dispose();

        _editorState.OnContentChanged -= HandleContentChanged;
        _editorState.OnSampleDataChanged -= HandleSampleDataChanged;
        _editorState.OnPreviewModeChanged -= HandlePreviewModeChanged;
        _editorState.OnTemplateLoaded -= HandleTemplateLoaded;
    }
}

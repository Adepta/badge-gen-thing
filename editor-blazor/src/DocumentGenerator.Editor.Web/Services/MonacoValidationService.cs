using Microsoft.JSInterop;

namespace DocumentGenerator.Editor.Web.Services;

/// <summary>
/// Manages Handlebars validation for Monaco editors.
/// Triggers validation, receives error markers, and exposes error count.
/// </summary>
public class MonacoValidationService : IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private IJSObjectReference? _module;
    private DotNetObjectReference<MonacoValidationService>? _dotnetRef;
    private int _errorCount;

    /// <summary>
    /// Fires when the validation error count changes.
    /// </summary>
    public event Action<int>? OnErrorCountChanged;

    /// <summary>
    /// Current number of validation errors.
    /// </summary>
    public int ErrorCount => _errorCount;

    public MonacoValidationService(IJSRuntime js)
    {
        _js = js;
    }

    private async Task<IJSObjectReference> GetModuleAsync()
    {
        if (_module is not null) return _module;

        await _js.InvokeVoidAsync("waitForMonaco");
        _module = await _js.InvokeAsync<IJSObjectReference>("import", "./js/monaco-validation.js");
        return _module;
    }

    private DotNetObjectReference<MonacoValidationService> GetDotNetRef()
    {
        _dotnetRef ??= DotNetObjectReference.Create(this);
        return _dotnetRef;
    }

    /// <summary>
    /// Starts validation for an editor instance.
    /// </summary>
    public async Task StartValidationAsync(string editorId)
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("startValidation", editorId, GetDotNetRef());
    }

    /// <summary>
    /// Stops validation for an editor instance.
    /// </summary>
    public async Task StopValidationAsync(string editorId)
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("stopValidation", editorId);
    }

    /// <summary>
    /// Triggers immediate validation of the given content.
    /// </summary>
    public async Task ValidateNowAsync(string editorId, string content)
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("validateNow", editorId, content);
    }

    /// <summary>
    /// Called from JavaScript when validation completes.
    /// </summary>
    [JSInvokable]
    public Task OnValidationComplete(string editorId, int errorCount)
    {
        _errorCount = errorCount;
        OnErrorCountChanged?.Invoke(errorCount);
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        _dotnetRef?.Dispose();

        if (_module is not null)
        {
            try
            {
                await _module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // Circuit disconnected
            }
        }
    }
}

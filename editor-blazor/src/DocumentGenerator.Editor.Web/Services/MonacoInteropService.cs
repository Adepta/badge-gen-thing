using Microsoft.JSInterop;

namespace DocumentGenerator.Editor.Web.Services;

/// <summary>
/// C# async wrapper for monaco-interop.js.
/// Lazily imports the JS module and awaits Monaco readiness on first use.
/// </summary>
public class MonacoInteropService : IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private IJSObjectReference? _module;
    private Task<IJSObjectReference>? _moduleTask;

    public MonacoInteropService(IJSRuntime js)
    {
        _js = js;
    }

    private async Task<IJSObjectReference> GetModuleAsync()
    {
        if (_module is not null) return _module;

        _moduleTask ??= LoadModuleAsync();
        _module = await _moduleTask;
        return _module;
    }

    private async Task<IJSObjectReference> LoadModuleAsync()
    {
        // Wait for Monaco to be ready (AMD loaded)
        await _js.InvokeVoidAsync("waitForMonaco");
        return await _js.InvokeAsync<IJSObjectReference>("import", "./js/monaco-interop.js");
    }

    public async Task<bool> CreateEditorAsync(string elementId, string editorId, string language, string value, object? options = null)
    {
        var module = await GetModuleAsync();
        return await module.InvokeAsync<bool>("createEditor", elementId, editorId, language, value, options);
    }

    public async Task DisposeEditorAsync(string editorId)
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("dispose", editorId);
    }

    public async Task<string> GetValueAsync(string editorId)
    {
        var module = await GetModuleAsync();
        return await module.InvokeAsync<string>("getValue", editorId);
    }

    public async Task SetValueAsync(string editorId, string value)
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("setValue", editorId, value);
    }

    public async Task SetValueAndClearUndoAsync(string editorId, string value)
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("setValueAndClearUndo", editorId, value);
    }

    public async Task SetThemeAsync(string theme)
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("setTheme", theme);
    }

    public async Task SetLanguageAsync(string editorId, string language)
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("setLanguage", editorId, language);
    }

    public async Task OnDidChangeContentAsync(string editorId, DotNetObjectReference<object> dotnetRef)
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("onDidChangeContent", editorId, dotnetRef);
    }

    public async Task OnDidChangeCursorPositionAsync(string editorId, DotNetObjectReference<object> dotnetRef)
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("onDidChangeCursorPosition", editorId, dotnetRef);
    }

    public async Task<CursorPosition> GetCursorPositionAsync(string editorId)
    {
        var module = await GetModuleAsync();
        return await module.InvokeAsync<CursorPosition>("getCursorPosition", editorId);
    }

    public async Task SetCursorPositionAsync(string editorId, int lineNumber, int column)
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("setCursorPosition", editorId, lineNumber, column);
    }

    public async Task InsertTextAsync(string editorId, string text)
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("insertText", editorId, text);
    }

    public async Task LayoutAsync(string editorId)
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("layout", editorId);
    }

    public async Task LayoutAllAsync()
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("layoutAll");
    }

    public async Task FocusAsync(string editorId)
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("focus", editorId);
    }

    public async Task SetModelMarkersAsync(string editorId, object[] markers)
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("setModelMarkers", editorId, markers);
    }

    public async Task UpdateOptionsAsync(string editorId, object options)
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("updateOptions", editorId, options);
    }

    public async Task AddCommandAsync(string editorId, int keybinding, DotNetObjectReference<object> dotnetRef, string methodName)
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("addCommand", editorId, keybinding, dotnetRef, methodName);
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            try
            {
                await _module.InvokeVoidAsync("disposeAll");
            }
            catch (JSDisconnectedException)
            {
                // Circuit disconnected, nothing to clean up
            }

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

/// <summary>
/// Represents a cursor position in the editor.
/// </summary>
public record CursorPosition(int LineNumber, int Column);

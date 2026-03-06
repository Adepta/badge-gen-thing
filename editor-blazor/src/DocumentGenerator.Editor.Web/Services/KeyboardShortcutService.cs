using Microsoft.JSInterop;

namespace DocumentGenerator.Editor.Web.Services;

/// <summary>
/// Registers global keyboard shortcuts via JS interop and dispatches to .NET handlers.
/// </summary>
public class KeyboardShortcutService : IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private IJSObjectReference? _module;
    private DotNetObjectReference<KeyboardShortcutService>? _dotnetRef;

    /// <summary>Fires when Ctrl+S is pressed.</summary>
    public event Func<Task>? OnSave;

    /// <summary>Fires when Ctrl+K is pressed.</summary>
    public event Func<Task>? OnCommandPalette;

    /// <summary>Fires when Ctrl+Q is pressed.</summary>
    public event Func<Task>? OnQuickParts;

    /// <summary>Fires when Escape is pressed.</summary>
    public event Func<Task>? OnEscape;

    public KeyboardShortcutService(IJSRuntime js)
    {
        _js = js;
    }

    /// <summary>
    /// Imports the keyboard interop module and registers the global keydown listener.
    /// </summary>
    public async Task InitializeAsync()
    {
        _dotnetRef = DotNetObjectReference.Create(this);
        _module = await _js.InvokeAsync<IJSObjectReference>("import", "./js/keyboard-interop.js");
        await _module.InvokeVoidAsync("initialize", _dotnetRef);
    }

    /// <summary>
    /// Called from JavaScript when a registered shortcut is pressed.
    /// </summary>
    [JSInvokable]
    public async Task OnShortcut(string action)
    {
        var handler = action switch
        {
            "save" => OnSave,
            "command-palette" => OnCommandPalette,
            "quick-parts" => OnQuickParts,
            "escape" => OnEscape,
            _ => null
        };

        if (handler is not null)
        {
            await handler.Invoke();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            try
            {
                await _module.InvokeVoidAsync("dispose");
                await _module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // Circuit disconnected
            }
        }

        _dotnetRef?.Dispose();
    }
}

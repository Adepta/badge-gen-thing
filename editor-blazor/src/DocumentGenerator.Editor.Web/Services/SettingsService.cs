using System.Text.Json;
using DocumentGenerator.Editor.Core.Models;
using Microsoft.JSInterop;

namespace DocumentGenerator.Editor.Web.Services;

/// <summary>
/// Manages user preferences with localStorage persistence via JS interop.
/// </summary>
public class SettingsService : IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private IJSObjectReference? _module;
    private EditorSettings _settings = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    /// <summary>Fires when settings are updated.</summary>
    public event Func<EditorSettings, Task>? OnSettingsChanged;

    public SettingsService(IJSRuntime js)
    {
        _js = js;
    }

    /// <summary>
    /// Loads settings from localStorage, returns EditorSettings (or defaults).
    /// </summary>
    public async Task<EditorSettings> InitializeAsync()
    {
        try
        {
            _module = await _js.InvokeAsync<IJSObjectReference>("import", "./js/settings-interop.js");
            var json = await _module.InvokeAsync<string?>("getSettings");

            if (!string.IsNullOrEmpty(json))
            {
                var loaded = JsonSerializer.Deserialize<EditorSettings>(json, JsonOptions);
                if (loaded is not null)
                {
                    _settings = loaded;
                }
            }
        }
        catch (JSException)
        {
            // localStorage not available or corrupt - use defaults
        }

        return _settings;
    }

    /// <summary>
    /// Serializes settings to JSON and saves to localStorage.
    /// </summary>
    public async Task SaveAsync(EditorSettings settings)
    {
        _settings = settings;

        if (_module is not null)
        {
            try
            {
                var json = JsonSerializer.Serialize(settings, JsonOptions);
                await _module.InvokeVoidAsync("saveSettings", json);
            }
            catch (JSException)
            {
                // localStorage write failed
            }
        }

        if (OnSettingsChanged is not null)
        {
            await OnSettingsChanged.Invoke(settings);
        }
    }

    /// <summary>
    /// Returns current settings.
    /// </summary>
    public EditorSettings GetSettings() => _settings;

    /// <summary>
    /// Returns current settings (async for compatibility).
    /// </summary>
    public Task<EditorSettings> GetSettingsAsync() => Task.FromResult(_settings);

    public async ValueTask DisposeAsync()
    {
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

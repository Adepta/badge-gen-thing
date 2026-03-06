using Microsoft.JSInterop;

namespace DocumentGenerator.Editor.Web.Services;

public class ThemeService : IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private IJSObjectReference? _module;
    private string _currentTheme = "system";
    private bool _initialized;

    public event Action? OnThemeChanged;

    public string CurrentTheme => _currentTheme;

    public ThemeService(IJSRuntime js)
    {
        _js = js ?? throw new ArgumentNullException(nameof(js));
    }

    public async Task InitializeAsync()
    {
        if (_initialized) return;

        _module = await _js.InvokeAsync<IJSObjectReference>("import", "./js/theme-interop.js");
        _currentTheme = await _module.InvokeAsync<string>("getTheme") ?? "system";
        _initialized = true;
    }

    public async Task SetThemeAsync(string theme)
    {
        if (_module is null) return;
        _currentTheme = theme;
        await _module.InvokeVoidAsync("setTheme", theme);
        OnThemeChanged?.Invoke();
    }

    public async Task ToggleThemeAsync()
    {
        var next = _currentTheme switch
        {
            "dark" => "light",
            "light" => "system",
            _ => "dark"
        };
        await SetThemeAsync(next);
    }

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

using Microsoft.JSInterop;

namespace DocumentGenerator.Editor.Web.Services;

/// <summary>
/// Registers custom Monaco languages and themes.
/// Ensures initialization happens only once per circuit.
/// </summary>
public class MonacoLanguageService : IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private IJSObjectReference? _languagesModule;
    private IJSObjectReference? _themesModule;
    private IJSObjectReference? _emmetModule;
    private IJSObjectReference? _colorsModule;
    private bool _initialized;

    public MonacoLanguageService(IJSRuntime js)
    {
        _js = js;
    }

    /// <summary>
    /// Imports and initializes custom languages, themes, and Emmet.
    /// Safe to call multiple times - only runs once.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_initialized) return;

        // Wait for Monaco to be loaded
        await _js.InvokeVoidAsync("waitForMonaco");

        // Register custom languages
        _languagesModule = await _js.InvokeAsync<IJSObjectReference>("import", "./js/monaco-languages.js");
        await _languagesModule.InvokeVoidAsync("registerLanguages");

        // Register custom themes
        _themesModule = await _js.InvokeAsync<IJSObjectReference>("import", "./js/monaco-themes.js");
        await _themesModule.InvokeVoidAsync("registerThemes");

        // Initialize Emmet support (best-effort)
        try
        {
            _emmetModule = await _js.InvokeAsync<IJSObjectReference>("import", "./js/emmet-interop.js");
            await _emmetModule.InvokeVoidAsync("initializeEmmet");
        }
        catch (JSException)
        {
            // Emmet initialization is optional - log and continue
        }

        // Register color providers for inline color pickers (best-effort)
        try
        {
            _colorsModule = await _js.InvokeAsync<IJSObjectReference>("import", "./js/monaco-colors.js");
            await _colorsModule.InvokeVoidAsync("registerColorProviders");
        }
        catch (JSException)
        {
            // Color provider initialization is optional
        }

        _initialized = true;
    }

    public bool IsInitialized => _initialized;

    /// <summary>
    /// Updates branding color values for the color picker.
    /// Call this when sample data changes so the color provider
    /// can resolve Handlebars branding tokens to their colors.
    /// </summary>
    public async Task UpdateBrandingColorsAsync(Dictionary<string, string> colors)
    {
        if (_colorsModule is not null)
        {
            try
            {
                await _colorsModule.InvokeVoidAsync("updateBrandingColors", colors);
            }
            catch (JSException)
            {
                // Color module may not be available
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_colorsModule is not null) await _colorsModule.DisposeAsync();
            if (_emmetModule is not null) await _emmetModule.DisposeAsync();
            if (_themesModule is not null) await _themesModule.DisposeAsync();
            if (_languagesModule is not null) await _languagesModule.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
            // Circuit disconnected
        }
    }
}

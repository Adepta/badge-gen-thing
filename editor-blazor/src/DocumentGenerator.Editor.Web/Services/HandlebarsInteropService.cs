using System.Text.Json;
using Microsoft.JSInterop;

namespace DocumentGenerator.Editor.Web.Services;

/// <summary>
/// C# async wrapper for handlebars-interop.js.
/// Lazily imports the JS module and registers Handlebars helpers on first use.
/// </summary>
public class HandlebarsInteropService : IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private IJSObjectReference? _module;
    private Task<IJSObjectReference>? _moduleTask;
    private bool _helpersRegistered;

    public HandlebarsInteropService(IJSRuntime js)
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
        return await _js.InvokeAsync<IJSObjectReference>("import", "./js/handlebars-interop.js");
    }

    /// <summary>
    /// Imports the JS module and registers all custom Handlebars helpers.
    /// </summary>
    public async Task InitializeAsync()
    {
        var module = await GetModuleAsync();
        if (!_helpersRegistered)
        {
            await module.InvokeVoidAsync("registerHelpers");
            _helpersRegistered = true;
        }
    }

    /// <summary>
    /// Compiles a Handlebars HTML template with the provided data.
    /// </summary>
    /// <param name="html">The Handlebars HTML template string.</param>
    /// <param name="data">The data object for rendering.</param>
    /// <returns>The rendered HTML string.</returns>
    public async Task<string> CompileAsync(string? html, object? data)
    {
        if (string.IsNullOrEmpty(html)) return string.Empty;

        await InitializeAsync();
        var module = await GetModuleAsync();
        return await module.InvokeAsync<string>("compile", html, data ?? new { });
    }

    /// <summary>
    /// Resolves CSS tokens (e.g., {{branding.primaryColour}}) with values from the data context.
    /// </summary>
    /// <param name="css">The CSS content with Handlebars tokens.</param>
    /// <param name="data">The data object for token resolution.</param>
    /// <returns>CSS with tokens replaced.</returns>
    public async Task<string> ResolveCssTokensAsync(string? css, object? data)
    {
        if (string.IsNullOrEmpty(css)) return string.Empty;

        await InitializeAsync();
        var module = await GetModuleAsync();
        return await module.InvokeAsync<string>("resolveCssTokens", css, data ?? new { });
    }

    /// <summary>
    /// Builds a complete HTML preview document with CSS and resolved templates.
    /// </summary>
    /// <param name="html">The HTML template content.</param>
    /// <param name="css">The CSS content.</param>
    /// <param name="data">The data context.</param>
    /// <param name="mode">"editor" or "live".</param>
    /// <returns>A complete HTML document string ready for iframe rendering.</returns>
    public async Task<string> BuildPreviewHtmlAsync(string? html, string? css, object? data, string mode)
    {
        if (string.IsNullOrEmpty(html) && string.IsNullOrEmpty(css))
            return string.Empty;

        await InitializeAsync();
        var module = await GetModuleAsync();
        return await module.InvokeAsync<string>("buildPreviewHtml",
            html ?? string.Empty,
            css ?? string.Empty,
            data ?? new { },
            mode ?? "editor");
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
                // Circuit disconnected, nothing to clean up
            }
        }
    }
}

using Microsoft.JSInterop;

namespace DocumentGenerator.Editor.Web.Services;

/// <summary>
/// Manages Monaco completion providers.
/// Registers auto-complete for Handlebars expressions and pushes dynamic tokens.
/// </summary>
public class MonacoCompletionService : IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private IJSObjectReference? _module;
    private bool _initialized;

    public MonacoCompletionService(IJSRuntime js)
    {
        _js = js;
    }

    /// <summary>
    /// Imports and registers completion providers.
    /// Safe to call multiple times - only runs once.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_initialized) return;

        // Wait for Monaco to be loaded
        await _js.InvokeVoidAsync("waitForMonaco");

        _module = await _js.InvokeAsync<IJSObjectReference>("import", "./js/monaco-completions.js");
        await _module.InvokeVoidAsync("registerCompletionProviders");

        _initialized = true;
    }

    /// <summary>
    /// Pushes updated dynamic tokens to the completion provider.
    /// Called when sample data changes so new keys appear in auto-complete.
    /// </summary>
    public async Task UpdateDynamicTokensAsync(IEnumerable<DynamicToken> tokens)
    {
        if (_module is null) return;
        await _module.InvokeVoidAsync("updateDynamicTokens", tokens.ToArray());
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

/// <summary>
/// Represents a dynamic completion token derived from sample data.
/// </summary>
public record DynamicToken(string Label, string Detail, string InsertText);

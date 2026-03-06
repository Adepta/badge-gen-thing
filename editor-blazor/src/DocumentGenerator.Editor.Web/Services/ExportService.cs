using Microsoft.JSInterop;

namespace DocumentGenerator.Editor.Web.Services;

/// <summary>
/// Wraps JS interop for triggering file downloads from the browser.
/// </summary>
public class ExportService : IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private IJSObjectReference? _module;

    public ExportService(IJSRuntime js)
    {
        _js = js ?? throw new ArgumentNullException(nameof(js));
    }

    /// <summary>
    /// Triggers a file download in the browser for the given URL.
    /// </summary>
    /// <param name="url">The URL to download from.</param>
    /// <param name="filename">The suggested filename for the download.</param>
    public async Task DownloadFileAsync(string url, string filename)
    {
        await EnsureModuleAsync();
        await _module!.InvokeVoidAsync("downloadFile", url, filename);
    }

    private async Task EnsureModuleAsync()
    {
        _module ??= await _js.InvokeAsync<IJSObjectReference>(
            "import", "./js/download-interop.js");
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            try
            {
                await _module.DisposeAsync();
            }
            catch
            {
                // Circuit may already be disconnected
            }
        }
    }
}

// Preview Interop for Blazor
// Manages Blob URL rendering for the preview iframe.

let _previousBlobUrl = null;

/**
 * Render HTML content in a sandboxed iframe using a Blob URL.
 * @param {string} iframeElementId - The DOM id of the iframe element.
 * @param {string} htmlContent - The complete HTML document to render.
 */
export function renderPreview(iframeElementId, htmlContent) {
    const iframe = document.getElementById(iframeElementId);
    if (!iframe) return;

    // Revoke previous blob URL to avoid memory leaks
    if (_previousBlobUrl) {
        URL.revokeObjectURL(_previousBlobUrl);
        _previousBlobUrl = null;
    }

    if (!htmlContent) {
        iframe.src = 'about:blank';
        return;
    }

    const blob = new Blob([htmlContent], { type: 'text/html' });
    _previousBlobUrl = URL.createObjectURL(blob);
    iframe.src = _previousBlobUrl;
}

/**
 * Clear the preview iframe and release any Blob URL.
 * @param {string} iframeElementId - The DOM id of the iframe element.
 */
export function clearPreview(iframeElementId) {
    const iframe = document.getElementById(iframeElementId);
    if (iframe) {
        iframe.src = 'about:blank';
    }
    if (_previousBlobUrl) {
        URL.revokeObjectURL(_previousBlobUrl);
        _previousBlobUrl = null;
    }
}

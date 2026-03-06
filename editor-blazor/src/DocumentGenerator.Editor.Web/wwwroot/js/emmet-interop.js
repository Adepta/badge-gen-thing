// Emmet Support for Monaco Editor
// Initializes Emmet abbreviation expansion for handlebars-html language

let _emmetDisposable = null;

export async function initializeEmmet() {
    // emmet-monaco-es is an npm package that needs to be bundled for browser use.
    // Since we're loading Monaco via AMD (require.js) and not bundling with webpack/vite,
    // we take a pragmatic approach: try to use emmet-monaco-es if available,
    // otherwise provide basic Emmet-like support through Monaco's built-in HTML features.

    try {
        // Attempt to load emmet-monaco-es as an ES module
        // This may work if the package is available as a browser-compatible ES module
        const emmet = await import('/node_modules/emmet-monaco-es/dist/emmet-monaco.esm.js').catch(() => null);
        
        if (emmet && emmet.emmetHTML) {
            _emmetDisposable = emmet.emmetHTML(monaco, ['handlebars-html', 'html']);
            console.log('Emmet initialized via emmet-monaco-es');
            return true;
        }
    } catch (e) {
        // Expected - emmet-monaco-es may not be loadable directly in browser
    }

    // TODO: Set up a proper bundling pipeline (webpack/vite) to bundle emmet-monaco-es
    // for browser consumption, or serve the pre-built browser bundle from wwwroot/lib/.
    // For now, Monaco's built-in HTML language provides basic tag completion.
    console.info('Emmet: emmet-monaco-es not available in browser context. ' +
        'HTML tag completion is provided by Monaco built-in features. ' +
        'To enable full Emmet, bundle emmet-monaco-es for the browser.');

    return false;
}

export function disposeEmmet() {
    if (_emmetDisposable) {
        _emmetDisposable.dispose();
        _emmetDisposable = null;
    }
}

// Quick Parts keyboard shortcut (Ctrl+Q)
// Registers a global keydown listener that calls back into Blazor

let _handler = null;

export function registerQuickPartsShortcut(dotnetRef) {
    // Remove any existing handler first
    unregisterQuickPartsShortcut();

    _handler = (e) => {
        if (e.ctrlKey && e.key === 'q') {
            e.preventDefault();
            e.stopPropagation();
            dotnetRef.invokeMethodAsync('ToggleQuickParts');
        }
    };

    document.addEventListener('keydown', _handler, true);
}

export function unregisterQuickPartsShortcut() {
    if (_handler) {
        document.removeEventListener('keydown', _handler, true);
        _handler = null;
    }
}

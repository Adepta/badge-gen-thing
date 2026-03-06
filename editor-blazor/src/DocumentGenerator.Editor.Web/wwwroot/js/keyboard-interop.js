/**
 * Global keyboard shortcut listener.
 * Registers a keydown handler and dispatches to .NET via DotNetObjectReference.
 */

let _dotnetRef = null;
let _handler = null;

export function initialize(dotnetRef) {
    _dotnetRef = dotnetRef;
    _handler = handleKeydown;
    document.addEventListener('keydown', _handler);
}

export function dispose() {
    if (_handler) {
        document.removeEventListener('keydown', _handler);
        _handler = null;
    }
    _dotnetRef = null;
}

function handleKeydown(e) {
    if (!_dotnetRef) return;

    const ctrl = e.ctrlKey || e.metaKey;

    if (ctrl && e.key === 's') {
        e.preventDefault();
        _dotnetRef.invokeMethodAsync('OnShortcut', 'save');
    }
    else if (ctrl && e.key === 'k') {
        e.preventDefault();
        _dotnetRef.invokeMethodAsync('OnShortcut', 'command-palette');
    }
    else if (ctrl && e.key === 'q') {
        e.preventDefault();
        _dotnetRef.invokeMethodAsync('OnShortcut', 'quick-parts');
    }
    else if (e.key === 'Escape') {
        _dotnetRef.invokeMethodAsync('OnShortcut', 'escape');
    }
}

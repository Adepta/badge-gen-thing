let _dotnetRef = null;
let _resizeHandler = null;
let _debounceTimer = null;

export function initialize(dotnetRef) {
    _dotnetRef = dotnetRef;
    notifySize();

    _resizeHandler = () => {
        // Debounce resize events
        if (_debounceTimer) clearTimeout(_debounceTimer);
        _debounceTimer = setTimeout(() => notifySize(), 100);
    };

    window.addEventListener('resize', _resizeHandler);
}

function notifySize() {
    if (_dotnetRef) {
        _dotnetRef.invokeMethodAsync('OnWindowResize', window.innerWidth, window.innerHeight);
    }
}

export function dispose() {
    if (_resizeHandler) {
        window.removeEventListener('resize', _resizeHandler);
        _resizeHandler = null;
    }
    if (_debounceTimer) {
        clearTimeout(_debounceTimer);
        _debounceTimer = null;
    }
    _dotnetRef = null;
}

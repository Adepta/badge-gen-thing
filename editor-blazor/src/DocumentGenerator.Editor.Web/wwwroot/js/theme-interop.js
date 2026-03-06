export function getTheme() {
    return localStorage.getItem('editor-theme') || 'system';
}

export function setTheme(theme) {
    localStorage.setItem('editor-theme', theme);
    applyTheme(theme);
}

export function applyTheme(theme) {
    const root = document.documentElement;
    if (theme === 'system') {
        root.removeAttribute('data-theme');
    } else {
        root.setAttribute('data-theme', theme);
    }

    // Also update Monaco editor theme if Monaco is loaded
    syncMonacoTheme(theme);
}

/**
 * Resolves the effective theme (dark or light) from the user's choice,
 * accounting for 'system' preference, and sets the Monaco editor theme.
 */
function syncMonacoTheme(theme) {
    if (typeof monaco === 'undefined' || !monaco?.editor?.setTheme) return;

    let resolved = theme;
    if (theme === 'system') {
        resolved = window.matchMedia('(prefers-color-scheme: light)').matches ? 'light' : 'dark';
    }

    const monacoTheme = resolved === 'light' ? 'editor-light' : 'editor-dark';
    monaco.editor.setTheme(monacoTheme);
}

// Listen for OS theme changes so 'system' mode stays in sync
window.matchMedia('(prefers-color-scheme: light)').addEventListener('change', () => {
    const current = getTheme();
    if (current === 'system') {
        syncMonacoTheme('system');
    }
});

// Apply on load
applyTheme(getTheme());

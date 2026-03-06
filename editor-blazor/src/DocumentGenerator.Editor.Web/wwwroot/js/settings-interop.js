/**
 * Settings persistence via localStorage.
 */

const STORAGE_KEY = 'editor-settings';

export function getSettings() {
    try {
        const json = localStorage.getItem(STORAGE_KEY);
        return json ? JSON.parse(json) : null;
    } catch {
        return null;
    }
}

export function saveSettings(settings) {
    try {
        localStorage.setItem(STORAGE_KEY, JSON.stringify(settings));
        return true;
    } catch {
        return false;
    }
}

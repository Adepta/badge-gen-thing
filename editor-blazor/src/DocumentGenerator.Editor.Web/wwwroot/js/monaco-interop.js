// Monaco Editor Interop for Blazor
// Manages editor instances, provides CRUD operations

const editors = new Map();

export function createEditor(elementId, editorId, language, value, options) {
    const container = document.getElementById(elementId);
    if (!container) {
        console.error(`Container element '${elementId}' not found`);
        return false;
    }

    const editorOptions = {
        value: value || '',
        language: language || 'html',
        theme: options?.theme || 'editor-dark',
        fontSize: options?.fontSize || 13,
        fontFamily: "'Geist Mono', monospace",
        lineHeight: 1.6,
        minimap: { enabled: options?.minimap ?? false },
        wordWrap: options?.wordWrap ? 'on' : 'off',
        tabSize: options?.tabSize || 2,
        lineNumbers: options?.lineNumbers !== false ? 'on' : 'off',
        scrollBeyondLastLine: false,
        automaticLayout: false,
        padding: { top: 12, bottom: 12 },
        renderLineHighlight: 'line',
        bracketPairColorization: { enabled: true },
        autoClosingBrackets: 'always',
        autoClosingQuotes: 'always',
        autoIndent: 'full',
        formatOnPaste: true,
        suggestOnTriggerCharacters: true,
        quickSuggestions: true,
        scrollbar: {
            verticalScrollbarSize: 6,
            horizontalScrollbarSize: 6,
            useShadows: false
        },
        overviewRulerLanes: 0,
        hideCursorInOverviewRuler: true,
        glyphMargin: true,
        folding: true,
        contextmenu: true,
        mouseWheelZoom: false,
        smoothScrolling: true,
        cursorBlinking: 'smooth',
        cursorSmoothCaretAnimation: 'on',
        renderWhitespace: 'none',
    };

    const editor = monaco.editor.create(container, editorOptions);
    editors.set(editorId, { editor, container });

    return true;
}

export function dispose(editorId) {
    const entry = editors.get(editorId);
    if (entry) {
        entry.editor.dispose();
        editors.delete(editorId);
    }
}

export function disposeAll() {
    for (const [id, entry] of editors) {
        entry.editor.dispose();
    }
    editors.clear();
}

export function getValue(editorId) {
    const entry = editors.get(editorId);
    return entry ? entry.editor.getValue() : '';
}

export function setValue(editorId, value) {
    const entry = editors.get(editorId);
    if (entry) {
        const editor = entry.editor;
        // Preserve undo stack by using executeEdits
        editor.executeEdits('blazor-interop', [{
            range: editor.getModel().getFullModelRange(),
            text: value
        }]);
    }
}

export function setValueAndClearUndo(editorId, value) {
    const entry = editors.get(editorId);
    if (entry) {
        entry.editor.getModel().setValue(value);
    }
}

export function setTheme(theme) {
    monaco.editor.setTheme(theme);
}

export function setLanguage(editorId, language) {
    const entry = editors.get(editorId);
    if (entry) {
        monaco.editor.setModelLanguage(entry.editor.getModel(), language);
    }
}

export function onDidChangeContent(editorId, dotnetRef) {
    const entry = editors.get(editorId);
    if (entry) {
        entry.editor.onDidChangeModelContent(() => {
            dotnetRef.invokeMethodAsync('OnEditorContentChanged', editorId);
        });
    }
}

export function onDidChangeCursorPosition(editorId, dotnetRef) {
    const entry = editors.get(editorId);
    if (entry) {
        entry.editor.onDidChangeCursorPosition((e) => {
            dotnetRef.invokeMethodAsync('OnCursorPositionChanged', editorId, e.position.lineNumber, e.position.column);
        });
    }
}

export function getCursorPosition(editorId) {
    const entry = editors.get(editorId);
    if (entry) {
        const pos = entry.editor.getPosition();
        return { lineNumber: pos.lineNumber, column: pos.column };
    }
    return { lineNumber: 1, column: 1 };
}

export function setCursorPosition(editorId, lineNumber, column) {
    const entry = editors.get(editorId);
    if (entry) {
        entry.editor.setPosition({ lineNumber, column });
        entry.editor.focus();
    }
}

export function insertText(editorId, text) {
    const entry = editors.get(editorId);
    if (entry) {
        const editor = entry.editor;
        const selection = editor.getSelection();
        editor.executeEdits('quick-parts', [{
            range: selection,
            text: text,
            forceMoveMarkers: true
        }]);
        editor.focus();
    }
}

export function layout(editorId) {
    const entry = editors.get(editorId);
    if (entry) {
        entry.editor.layout();
    }
}

export function layoutAll() {
    for (const [id, entry] of editors) {
        entry.editor.layout();
    }
}

export function focus(editorId) {
    const entry = editors.get(editorId);
    if (entry) {
        entry.editor.focus();
    }
}

export function getModel(editorId) {
    const entry = editors.get(editorId);
    return entry ? entry.editor.getModel() : null;
}

export function setModelMarkers(editorId, markers) {
    const entry = editors.get(editorId);
    if (entry) {
        const model = entry.editor.getModel();
        if (model) {
            monaco.editor.setModelMarkers(model, 'handlebars-validation', markers);
        }
    }
}

export function updateOptions(editorId, options) {
    const entry = editors.get(editorId);
    if (entry) {
        entry.editor.updateOptions(options);
    }
}

export function addCommand(editorId, keybinding, dotnetRef, methodName) {
    const entry = editors.get(editorId);
    if (entry) {
        entry.editor.addCommand(keybinding, () => {
            dotnetRef.invokeMethodAsync(methodName);
        });
    }
}

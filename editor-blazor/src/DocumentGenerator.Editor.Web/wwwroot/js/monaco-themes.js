// Monaco Editor Custom Themes
// Matches the app's color system from the design spec

export function registerThemes() {
    // ── Dark Theme ──
    monaco.editor.defineTheme('editor-dark', {
        base: 'vs-dark',
        inherit: true,
        rules: [
            // General
            { token: '', foreground: 'ededef', background: '141417' },
            { token: 'comment', foreground: '6b6b76', fontStyle: 'italic' },
            { token: 'comment.html', foreground: '6b6b76', fontStyle: 'italic' },
            { token: 'comment.content', foreground: '6b6b76', fontStyle: 'italic' },

            // HTML
            { token: 'tag', foreground: '7dd3fc' },
            { token: 'tag.html', foreground: '7dd3fc' },
            { token: 'metatag', foreground: '7dd3fc' },
            { token: 'metatag.html', foreground: '7dd3fc' },
            { token: 'metatag.content.html', foreground: 'ededef' },
            { token: 'attribute.name', foreground: 'c4b5fd' },
            { token: 'attribute.name.html', foreground: 'c4b5fd' },
            { token: 'attribute.value', foreground: '86efac' },
            { token: 'attribute.value.html', foreground: '86efac' },
            { token: 'delimiter.html', foreground: '6b6b76' },

            // Strings
            { token: 'string', foreground: '86efac' },
            { token: 'string.html', foreground: '86efac' },
            { token: 'string.value', foreground: '86efac' },

            // CSS
            { token: 'tag.css', foreground: '7dd3fc' },
            { token: 'attribute.name.css', foreground: '93c5fd' },
            { token: 'attribute.value.css', foreground: 'fca5a5' },
            { token: 'attribute.value.number.css', foreground: 'fcd34d' },
            { token: 'attribute.value.unit.css', foreground: 'fca5a5' },
            { token: 'attribute.value.hex.css', foreground: 'fca5a5' },
            { token: 'delimiter.css', foreground: '6b6b76' },
            { token: 'delimiter.parenthesis.css', foreground: '6b6b76' },
            { token: 'keyword.css', foreground: 'f472b6' },
            { token: 'number.css', foreground: 'fcd34d' },

            // Handlebars
            { token: 'delimiter.handlebars', foreground: 'c084fc' },
            { token: 'variable.handlebars', foreground: 'c084fc' },
            { token: 'keyword.handlebars', foreground: 'fb923c' },
            { token: 'keyword.helper.handlebars', foreground: 'fb923c' },
            { token: 'comment.handlebars', foreground: '6b6b76', fontStyle: 'italic' },
            { token: 'string.handlebars', foreground: '86efac' },
            { token: 'raw.handlebars', foreground: 'fcd34d' },

            // Numbers
            { token: 'number', foreground: 'fcd34d' },
            { token: 'number.hex', foreground: 'fcd34d' },

            // Keywords
            { token: 'keyword', foreground: 'f472b6' },

            // Operators
            { token: 'operator', foreground: 'a0a0ab' },
            { token: 'delimiter', foreground: '6b6b76' },
        ],
        colors: {
            'editor.background': '#141417',
            'editor.foreground': '#ededef',
            'editor.lineHighlightBackground': '#1c1c21',
            'editor.selectionBackground': 'rgba(139,92,246,0.25)',
            'editor.inactiveSelectionBackground': 'rgba(139,92,246,0.12)',
            'editorLineNumber.foreground': '#6b6b76',
            'editorLineNumber.activeForeground': '#a0a0ab',
            'editorCursor.foreground': '#8b5cf6',
            'editorIndentGuide.background': '#1e1e25',
            'editorIndentGuide.activeBackground': '#2a2a32',
            'editorBracketMatch.background': 'rgba(139,92,246,0.15)',
            'editorBracketMatch.border': '#8b5cf6',
            'editorGutter.background': '#141417',
            'editor.selectionHighlightBackground': 'rgba(139,92,246,0.12)',
            'editorOverviewRuler.border': '#00000000',
            'scrollbar.shadow': '#00000000',
            'scrollbarSlider.background': 'rgba(255,255,255,0.08)',
            'scrollbarSlider.hoverBackground': 'rgba(255,255,255,0.15)',
            'scrollbarSlider.activeBackground': 'rgba(255,255,255,0.2)',
            'editorWidget.background': '#1c1c21',
            'editorWidget.border': '#2a2a32',
            'editorSuggestWidget.background': '#1c1c21',
            'editorSuggestWidget.border': '#2a2a32',
            'editorSuggestWidget.selectedBackground': 'rgba(139,92,246,0.15)',
            'editorSuggestWidget.highlightForeground': '#8b5cf6',
            'editorHoverWidget.background': '#1c1c21',
            'editorHoverWidget.border': '#2a2a32',
            'input.background': '#0a0a0b',
            'input.border': '#2a2a32',
            'input.foreground': '#ededef',
            'focusBorder': '#8b5cf6',
            'list.hoverBackground': 'rgba(139,92,246,0.08)',
            'list.activeSelectionBackground': 'rgba(139,92,246,0.15)',
        }
    });

    // ── Light Theme ──
    monaco.editor.defineTheme('editor-light', {
        base: 'vs',
        inherit: true,
        rules: [
            // General
            { token: '', foreground: '111113', background: 'f8f8fa' },
            { token: 'comment', foreground: 'a0a0a8', fontStyle: 'italic' },
            { token: 'comment.html', foreground: 'a0a0a8', fontStyle: 'italic' },
            { token: 'comment.content', foreground: 'a0a0a8', fontStyle: 'italic' },

            // HTML
            { token: 'tag', foreground: '0369a1' },
            { token: 'tag.html', foreground: '0369a1' },
            { token: 'metatag', foreground: '0369a1' },
            { token: 'metatag.html', foreground: '0369a1' },
            { token: 'metatag.content.html', foreground: '111113' },
            { token: 'attribute.name', foreground: '6d28d9' },
            { token: 'attribute.name.html', foreground: '6d28d9' },
            { token: 'attribute.value', foreground: '15803d' },
            { token: 'attribute.value.html', foreground: '15803d' },
            { token: 'delimiter.html', foreground: 'a0a0a8' },

            // Strings
            { token: 'string', foreground: '15803d' },
            { token: 'string.html', foreground: '15803d' },
            { token: 'string.value', foreground: '15803d' },

            // CSS
            { token: 'tag.css', foreground: '0369a1' },
            { token: 'attribute.name.css', foreground: '1d4ed8' },
            { token: 'attribute.value.css', foreground: 'b91c1c' },
            { token: 'attribute.value.number.css', foreground: 'b45309' },
            { token: 'attribute.value.unit.css', foreground: 'b91c1c' },
            { token: 'attribute.value.hex.css', foreground: 'b91c1c' },
            { token: 'delimiter.css', foreground: 'a0a0a8' },
            { token: 'delimiter.parenthesis.css', foreground: 'a0a0a8' },
            { token: 'keyword.css', foreground: 'be185d' },
            { token: 'number.css', foreground: 'b45309' },

            // Handlebars
            { token: 'delimiter.handlebars', foreground: '7c3aed' },
            { token: 'variable.handlebars', foreground: '7c3aed' },
            { token: 'keyword.handlebars', foreground: 'c2410c' },
            { token: 'keyword.helper.handlebars', foreground: 'c2410c' },
            { token: 'comment.handlebars', foreground: 'a0a0a8', fontStyle: 'italic' },
            { token: 'string.handlebars', foreground: '15803d' },
            { token: 'raw.handlebars', foreground: 'b45309' },

            // Numbers
            { token: 'number', foreground: 'b45309' },
            { token: 'number.hex', foreground: 'b45309' },

            // Keywords
            { token: 'keyword', foreground: 'be185d' },

            // Operators
            { token: 'operator', foreground: '65656d' },
            { token: 'delimiter', foreground: 'a0a0a8' },
        ],
        colors: {
            'editor.background': '#f8f8fa',
            'editor.foreground': '#111113',
            'editor.lineHighlightBackground': '#f0f0f3',
            'editor.selectionBackground': 'rgba(124,58,237,0.15)',
            'editor.inactiveSelectionBackground': 'rgba(124,58,237,0.08)',
            'editorLineNumber.foreground': '#a0a0a8',
            'editorLineNumber.activeForeground': '#65656d',
            'editorCursor.foreground': '#7c3aed',
            'editorIndentGuide.background': '#ededf0',
            'editorIndentGuide.activeBackground': '#e2e2e8',
            'editorBracketMatch.background': 'rgba(124,58,237,0.1)',
            'editorBracketMatch.border': '#7c3aed',
            'editorGutter.background': '#f8f8fa',
            'editor.selectionHighlightBackground': 'rgba(124,58,237,0.08)',
            'editorOverviewRuler.border': '#00000000',
            'scrollbar.shadow': '#00000000',
            'scrollbarSlider.background': 'rgba(0,0,0,0.08)',
            'scrollbarSlider.hoverBackground': 'rgba(0,0,0,0.15)',
            'scrollbarSlider.activeBackground': 'rgba(0,0,0,0.2)',
            'editorWidget.background': '#ffffff',
            'editorWidget.border': '#e2e2e8',
            'editorSuggestWidget.background': '#ffffff',
            'editorSuggestWidget.border': '#e2e2e8',
            'editorSuggestWidget.selectedBackground': 'rgba(124,58,237,0.1)',
            'editorSuggestWidget.highlightForeground': '#7c3aed',
            'editorHoverWidget.background': '#ffffff',
            'editorHoverWidget.border': '#e2e2e8',
            'input.background': '#ffffff',
            'input.border': '#e2e2e8',
            'input.foreground': '#111113',
            'focusBorder': '#7c3aed',
            'list.hoverBackground': 'rgba(124,58,237,0.05)',
            'list.activeSelectionBackground': 'rgba(124,58,237,0.1)',
        }
    });
}

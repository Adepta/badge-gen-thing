// Monaco Editor Handlebars Validation
// Parses content for Handlebars syntax errors and reports markers

const _validationTimers = new Map();
const _dotnetRefs = new Map();

const KNOWN_HELPERS = new Set([
    'if', 'unless', 'each', 'with', 'ifEquals', 'lookup', 'log',
    'upper', 'lower', 'formatDate', 'currency', 'qrCode', 'barCode', 'else'
]);

const BLOCK_HELPERS = new Set([
    'if', 'unless', 'each', 'with', 'ifEquals', 'lookup'
]);

function validate(content) {
    const markers = [];
    const lines = content.split('\n');
    const blockStack = [];

    for (let i = 0; i < lines.length; i++) {
        const line = lines[i];
        const lineNumber = i + 1;

        // Find all handlebars expressions in this line
        let pos = 0;
        while (pos < line.length) {
            // Look for {{ or {{{
            const idx = line.indexOf('{{', pos);
            if (idx === -1) break;

            const isTriple = line[idx + 2] === '{';
            const isComment = line[idx + 2] === '!';
            const isBlockClose = line[idx + 2] === '/';
            const isBlockOpen = line[idx + 2] === '#';

            // Handle comment {{!-- ... --}}
            if (isComment) {
                const dashComment = line.substring(idx).startsWith('{{!--');
                if (dashComment) {
                    // Look for --}} (may span lines, but we only check single-line for simplicity)
                    const endIdx = line.indexOf('--}}', idx + 5);
                    if (endIdx !== -1) {
                        pos = endIdx + 4;
                    } else {
                        // Multi-line comment - skip to end of line
                        pos = line.length;
                    }
                } else {
                    // Short comment {{! ... }}
                    const endIdx = line.indexOf('}}', idx + 3);
                    if (endIdx === -1) {
                        markers.push({
                            severity: monaco.MarkerSeverity.Error,
                            message: 'Unclosed Handlebars comment',
                            startLineNumber: lineNumber,
                            startColumn: idx + 1,
                            endLineNumber: lineNumber,
                            endColumn: line.length + 1
                        });
                        pos = line.length;
                    } else {
                        pos = endIdx + 2;
                    }
                }
                continue;
            }

            // Find closing braces
            let closeStr = isTriple ? '}}}' : '}}';
            let searchStart = idx + (isTriple ? 3 : 2);
            let endIdx = line.indexOf(closeStr, searchStart);

            if (endIdx === -1) {
                // Check if we at least have }} for a triple
                if (isTriple) {
                    const doubleEnd = line.indexOf('}}', searchStart);
                    if (doubleEnd !== -1) {
                        markers.push({
                            severity: monaco.MarkerSeverity.Warning,
                            message: 'Triple-stache {{{ requires closing }}}',
                            startLineNumber: lineNumber,
                            startColumn: idx + 1,
                            endLineNumber: lineNumber,
                            endColumn: doubleEnd + 3
                        });
                        pos = doubleEnd + 2;
                        continue;
                    }
                }

                markers.push({
                    severity: monaco.MarkerSeverity.Error,
                    message: `Unclosed Handlebars expression (missing ${closeStr})`,
                    startLineNumber: lineNumber,
                    startColumn: idx + 1,
                    endLineNumber: lineNumber,
                    endColumn: line.length + 1
                });
                pos = line.length;
                continue;
            }

            // Extract the expression content
            const exprContent = line.substring(searchStart, endIdx).trim();

            // Block open: {{#helper ...}}
            if (isBlockOpen) {
                const helperMatch = exprContent.match(/^(\w+)/);
                if (helperMatch) {
                    const helperName = helperMatch[1];
                    if (!BLOCK_HELPERS.has(helperName)) {
                        markers.push({
                            severity: monaco.MarkerSeverity.Warning,
                            message: `Unknown block helper: #${helperName}`,
                            startLineNumber: lineNumber,
                            startColumn: idx + 1,
                            endLineNumber: lineNumber,
                            endColumn: endIdx + closeStr.length + 1
                        });
                    }
                    blockStack.push({
                        name: helperName,
                        line: lineNumber,
                        column: idx + 1
                    });
                }
            }
            // Block close: {{/helper}}
            else if (isBlockClose) {
                const helperMatch = exprContent.match(/^(\w+)/);
                if (helperMatch) {
                    const helperName = helperMatch[1];
                    if (blockStack.length === 0) {
                        markers.push({
                            severity: monaco.MarkerSeverity.Error,
                            message: `Closing {{/${helperName}}} without matching opening block`,
                            startLineNumber: lineNumber,
                            startColumn: idx + 1,
                            endLineNumber: lineNumber,
                            endColumn: endIdx + closeStr.length + 1
                        });
                    } else {
                        const top = blockStack[blockStack.length - 1];
                        if (top.name !== helperName) {
                            markers.push({
                                severity: monaco.MarkerSeverity.Error,
                                message: `Mismatched block: expected {{/${top.name}}} but found {{/${helperName}}}`,
                                startLineNumber: lineNumber,
                                startColumn: idx + 1,
                                endLineNumber: lineNumber,
                                endColumn: endIdx + closeStr.length + 1
                            });
                        }
                        blockStack.pop();
                    }
                }
            }
            // Regular expression: check for unknown helpers
            else if (!isTriple && !isBlockOpen && !isBlockClose) {
                const helperMatch = exprContent.match(/^(\w+)\s+/);
                if (helperMatch) {
                    const helperName = helperMatch[1];
                    // Only warn if it looks like a helper call (has arguments) and isn't known
                    if (!KNOWN_HELPERS.has(helperName) && !helperName.includes('.')) {
                        markers.push({
                            severity: monaco.MarkerSeverity.Info,
                            message: `Unknown helper: ${helperName}`,
                            startLineNumber: lineNumber,
                            startColumn: idx + 1,
                            endLineNumber: lineNumber,
                            endColumn: endIdx + closeStr.length + 1
                        });
                    }
                }
            }

            // Warn on triple-stache with non-raw helpers
            if (isTriple) {
                const helperMatch = exprContent.match(/^(\w+)/);
                if (helperMatch) {
                    const name = helperMatch[1];
                    if (!['qrCode', 'barCode'].includes(name) && !name.includes('.')) {
                        markers.push({
                            severity: monaco.MarkerSeverity.Warning,
                            message: `Triple-stache {{{...}}} outputs unescaped HTML. Use {{...}} unless raw output is intended.`,
                            startLineNumber: lineNumber,
                            startColumn: idx + 1,
                            endLineNumber: lineNumber,
                            endColumn: endIdx + closeStr.length + 1
                        });
                    }
                }
            }

            pos = endIdx + closeStr.length;
        }
    }

    // Report unclosed blocks
    for (const block of blockStack) {
        markers.push({
            severity: monaco.MarkerSeverity.Error,
            message: `Unclosed block: {{#${block.name}}} has no matching {{/${block.name}}}`,
            startLineNumber: block.line,
            startColumn: block.column,
            endLineNumber: block.line,
            endColumn: block.column + block.name.length + 5
        });
    }

    return markers;
}

export function startValidation(editorId, dotnetRef) {
    _dotnetRefs.set(editorId, dotnetRef);

    // Import monaco-interop to access editors (we use monaco global API instead)
    // Find the editor model through Monaco's global editor API
    const models = monaco.editor.getModels();
    const editors = monaco.editor.getEditors();
    
    // Find the editor by checking all editor instances
    let targetEditor = null;
    for (const editor of editors) {
        // We identify by container element id convention
        const container = editor.getDomNode()?.parentElement;
        if (container && container.id && container.id.includes(editorId.replace('editor-', ''))) {
            targetEditor = editor;
            break;
        }
    }

    // Fallback: validate all content changes on any model
    // The validation will be triggered from C# via validateNow
}

export function stopValidation(editorId) {
    const timerId = _validationTimers.get(editorId);
    if (timerId) {
        clearTimeout(timerId);
        _validationTimers.delete(editorId);
    }
    _dotnetRefs.delete(editorId);
}

export function validateNow(editorId, content) {
    // Clear any pending debounce
    const existingTimer = _validationTimers.get(editorId);
    if (existingTimer) {
        clearTimeout(existingTimer);
    }

    // Debounce validation at 500ms
    const timerId = setTimeout(() => {
        const markers = validate(content || '');
        
        // Set markers on the editor model
        const editors = monaco.editor.getEditors();
        for (const editor of editors) {
            const container = editor.getDomNode()?.parentElement;
            if (container && container.id) {
                const model = editor.getModel();
                if (model && model.getValue() === content) {
                    monaco.editor.setModelMarkers(model, 'handlebars-validation', markers);
                    break;
                }
            }
        }

        // Report error count back to C#
        const dotnetRef = _dotnetRefs.get(editorId);
        if (dotnetRef) {
            const errorCount = markers.filter(m => m.severity === monaco.MarkerSeverity.Error).length;
            dotnetRef.invokeMethodAsync('OnValidationComplete', editorId, errorCount);
        }

        _validationTimers.delete(editorId);
    }, 500);

    _validationTimers.set(editorId, timerId);
}

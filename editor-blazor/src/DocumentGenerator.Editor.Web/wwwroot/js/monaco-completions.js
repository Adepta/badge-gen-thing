// Monaco Editor Completion Providers
// Provides auto-complete for Handlebars expressions in HTML and CSS editors

let _dynamicTokens = [];

const variableTokens = [
    { label: 'variables.firstName', detail: 'First name', insertText: 'variables.firstName}}' },
    { label: 'variables.lastName', detail: 'Last name', insertText: 'variables.lastName}}' },
    { label: 'variables.jobTitle', detail: 'Job title', insertText: 'variables.jobTitle}}' },
    { label: 'variables.company', detail: 'Company name', insertText: 'variables.company}}' },
    { label: 'variables.attendeeId', detail: 'Attendee ID', insertText: 'variables.attendeeId}}' },
    { label: 'variables.ticketType', detail: 'Ticket type', insertText: 'variables.ticketType}}' },
    { label: 'variables.sessionName', detail: 'Session name', insertText: 'variables.sessionName}}' },
    { label: 'variables.eventDate', detail: 'Event date', insertText: 'variables.eventDate}}' },
    { label: 'variables.eventVenue', detail: 'Event venue', insertText: 'variables.eventVenue}}' },
];

const brandingTokens = [
    { label: 'branding.companyName', detail: 'Company / event name', insertText: 'branding.companyName}}' },
    { label: 'branding.primaryColour', detail: 'Primary brand colour', insertText: 'branding.primaryColour}}', isColour: true },
    { label: 'branding.secondaryColour', detail: 'Secondary brand colour', insertText: 'branding.secondaryColour}}', isColour: true },
    { label: 'branding.bodyFont', detail: 'Body font family', insertText: 'branding.bodyFont}}', isFont: true },
    { label: 'branding.custom.accentColour', detail: 'Accent colour', insertText: 'branding.custom.accentColour}}', isColour: true },
];

const helperTokens = [
    { label: 'upper', detail: 'Uppercase text', insertText: 'upper ${1:variables.firstName}}}' },
    { label: 'lower', detail: 'Lowercase text', insertText: 'lower ${1:variables.ticketType}}}' },
    { label: 'formatDate', detail: 'Format a date', insertText: 'formatDate ${1:variables.eventDate} "${2:DD MMM YYYY}"}}'  },
    { label: 'currency', detail: 'Format as currency', insertText: 'currency ${1:variables.price} "${2:GBP}"}}'  },
    { label: 'ifEquals', detail: 'Conditional comparison', insertText: 'ifEquals ${1:variables.ticketType} "${2:VIP}"}}${3}{{/ifEquals}}' },
    { label: 'qrCode', detail: 'QR code SVG (triple-stache)', insertText: '{qrCode ${1:variables.attendeeId} "${2:#ffffff}" "${3:transparent}"}}}' },
    { label: 'barCode', detail: 'Barcode SVG (triple-stache)', insertText: '{barCode ${1:variables.attendeeId}}}}' },
];

const blockHelperSnippets = [
    {
        label: '#ifEquals ... /ifEquals',
        detail: 'Conditional block',
        insertText: '#ifEquals ${1:variables.ticketType} "${2:VIP}"}}\n\t${3:<!-- VIP content -->}\n{{/ifEquals}}',
    },
    {
        label: '#each ... /each',
        detail: 'Loop block',
        insertText: '#each ${1:items}}}\n\t{{this}}\n{{/each}}',
    },
    {
        label: '#unless ... /unless',
        detail: 'Unless block',
        insertText: '#unless ${1:variables.hidden}}}\n\t${2:<!-- shown when falsy -->}\n{{/unless}}',
    },
    {
        label: '#if ... /if',
        detail: 'If block',
        insertText: '#if ${1:variables.showSection}}}\n\t${2:<!-- conditional content -->}\n{{/if}}',
    },
];

function createCompletionItem(token, range, kind, sortPrefix) {
    return {
        label: token.label,
        kind: kind,
        detail: token.detail || '',
        insertText: token.insertText,
        insertTextRules: token.insertText.includes('${')
            ? monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet
            : monaco.languages.CompletionItemInsertTextRule.None,
        range: range,
        sortText: sortPrefix + token.label,
    };
}

function getHtmlCompletions(model, position) {
    const textUntilPosition = model.getValueInRange({
        startLineNumber: position.lineNumber,
        startColumn: 1,
        endLineNumber: position.lineNumber,
        endColumn: position.column
    });

    // Check if we're inside a {{ expression
    const lastOpen = textUntilPosition.lastIndexOf('{{');
    const lastClose = textUntilPosition.lastIndexOf('}}');

    if (lastOpen === -1 || lastClose > lastOpen) {
        return { suggestions: [] };
    }

    // Determine how much of the expression has been typed
    const afterBraces = textUntilPosition.substring(lastOpen + 2);
    const wordMatch = afterBraces.match(/^#?[\w.]*/);
    const word = wordMatch ? wordMatch[0] : '';

    const startColumn = position.column - word.length;
    const range = {
        startLineNumber: position.lineNumber,
        startColumn: startColumn,
        endLineNumber: position.lineNumber,
        endColumn: position.column
    };

    const suggestions = [];

    // Variables
    for (const token of variableTokens) {
        suggestions.push(createCompletionItem(token, range, monaco.languages.CompletionItemKind.Variable, 'a_'));
    }

    // Branding
    for (const token of brandingTokens) {
        suggestions.push(createCompletionItem(token, range, monaco.languages.CompletionItemKind.Color, 'b_'));
    }

    // Helpers
    for (const token of helperTokens) {
        suggestions.push(createCompletionItem(token, range, monaco.languages.CompletionItemKind.Function, 'c_'));
    }

    // Block helpers (snippets)
    for (const token of blockHelperSnippets) {
        suggestions.push(createCompletionItem(token, range, monaco.languages.CompletionItemKind.Snippet, 'd_'));
    }

    // Dynamic tokens from sample data
    for (const token of _dynamicTokens) {
        suggestions.push(createCompletionItem(
            { label: token.label, detail: token.detail || 'Dynamic token', insertText: token.insertText || (token.label + '}}') },
            range,
            monaco.languages.CompletionItemKind.Field,
            'e_'
        ));
    }

    return { suggestions };
}

function getCssCompletions(model, position) {
    const textUntilPosition = model.getValueInRange({
        startLineNumber: position.lineNumber,
        startColumn: 1,
        endLineNumber: position.lineNumber,
        endColumn: position.column
    });

    // Check if we're inside a {{ expression
    const lastOpen = textUntilPosition.lastIndexOf('{{');
    const lastClose = textUntilPosition.lastIndexOf('}}');

    if (lastOpen === -1 || lastClose > lastOpen) {
        return { suggestions: [] };
    }

    const afterBraces = textUntilPosition.substring(lastOpen + 2);
    const wordMatch = afterBraces.match(/^[\w.]*/);
    const word = wordMatch ? wordMatch[0] : '';

    const startColumn = position.column - word.length;
    const range = {
        startLineNumber: position.lineNumber,
        startColumn: startColumn,
        endLineNumber: position.lineNumber,
        endColumn: position.column
    };

    // Determine CSS context by looking at the line content before {{
    const beforeBraces = textUntilPosition.substring(0, lastOpen).trim();
    const isColorContext = /(?:color|background|border|outline|shadow|fill|stroke)\s*:/i.test(beforeBraces);
    const isFontContext = /font-family\s*:/i.test(beforeBraces);

    const suggestions = [];

    if (isFontContext) {
        // Prioritize font tokens
        for (const token of brandingTokens) {
            if (token.isFont) {
                suggestions.push(createCompletionItem(token, range, monaco.languages.CompletionItemKind.Value, 'a_'));
            }
        }
        // Then show the rest
        for (const token of brandingTokens) {
            if (!token.isFont) {
                suggestions.push(createCompletionItem(token, range, monaco.languages.CompletionItemKind.Value, 'b_'));
            }
        }
    } else if (isColorContext) {
        // Prioritize colour tokens
        for (const token of brandingTokens) {
            if (token.isColour) {
                suggestions.push(createCompletionItem(token, range, monaco.languages.CompletionItemKind.Color, 'a_'));
            }
        }
        // Then show the rest
        for (const token of brandingTokens) {
            if (!token.isColour) {
                suggestions.push(createCompletionItem(token, range, monaco.languages.CompletionItemKind.Value, 'b_'));
            }
        }
    } else {
        // Show all branding tokens
        for (const token of brandingTokens) {
            suggestions.push(createCompletionItem(token, range, monaco.languages.CompletionItemKind.Value, 'a_'));
        }
    }

    return { suggestions };
}

export function registerCompletionProviders() {
    // HTML editor completions
    monaco.languages.registerCompletionItemProvider('handlebars-html', {
        triggerCharacters: ['{'],
        provideCompletionItems: (model, position) => {
            return getHtmlCompletions(model, position);
        }
    });

    // CSS editor completions
    monaco.languages.registerCompletionItemProvider('handlebars-css', {
        triggerCharacters: ['{'],
        provideCompletionItems: (model, position) => {
            return getCssCompletions(model, position);
        }
    });
}

export function updateDynamicTokens(tokens) {
    _dynamicTokens = tokens || [];
}

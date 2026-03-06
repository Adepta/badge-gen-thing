// Monaco Editor Custom Languages
// Registers handlebars-html and handlebars-css with Monarch tokenizers

export function registerLanguages() {
    // ── Register handlebars-html language ──
    monaco.languages.register({
        id: 'handlebars-html',
        extensions: ['.hbs', '.handlebars'],
        aliases: ['Handlebars HTML', 'hbs-html'],
        mimetypes: ['text/x-handlebars-html']
    });

    monaco.languages.setMonarchTokensProvider('handlebars-html', {
        defaultToken: '',
        tokenPostfix: '',
        ignoreCase: true,

        // Helper keywords used in block expressions
        blockHelpers: ['if', 'unless', 'each', 'with', 'ifEquals', 'lookup', 'log'],
        inlineHelpers: ['upper', 'lower', 'formatDate', 'currency', 'qrCode', 'barCode'],

        tokenizer: {
            root: [
                // Handlebars comment {{!-- ... --}}
                [/\{\{!--/, 'comment.handlebars', '@hbsComment'],
                // Handlebars comment {{! ... }}
                [/\{\{!/, 'comment.handlebars', '@hbsShortComment'],
                // Raw triple-stache {{{ ... }}}
                [/\{\{\{/, 'delimiter.handlebars', '@hbsRawExpression'],
                // Block close {{/ ... }}
                [/\{\{\//, 'delimiter.handlebars', '@hbsBlockClose'],
                // Block open {{# ... }}
                [/\{\{#/, 'delimiter.handlebars', '@hbsBlockOpen'],
                // Else {{else}}
                [/\{\{else\}\}/, 'keyword.handlebars'],
                // Expression {{ ... }}
                [/\{\{/, 'delimiter.handlebars', '@hbsExpression'],
                // HTML comment
                [/<!--/, 'comment.html', '@htmlComment'],
                // DOCTYPE
                [/<!DOCTYPE/i, 'metatag.html', '@doctype'],
                // HTML tags
                [/(<)((?:[\w\-]+:)?[\w\-]+)(\s*)(\/>)/, ['delimiter.html', 'tag.html', '', 'delimiter.html']],
                [/(<)(script)/, ['delimiter.html', { token: 'tag.html', next: '@script' }]],
                [/(<)(style)/, ['delimiter.html', { token: 'tag.html', next: '@style' }]],
                [/(<)((?:[\w\-]+:)?[\w\-]+)/, ['delimiter.html', { token: 'tag.html', next: '@htmlTag' }]],
                [/(<\/)((?:[\w\-]+:)?[\w\-]+)/, ['delimiter.html', { token: 'tag.html', next: '@htmlEndTag' }]],
                // Content text
                [/[^<{]+/, ''],
            ],

            // ── Handlebars States ──
            hbsComment: [
                [/--\}\}/, 'comment.handlebars', '@pop'],
                [/./, 'comment.handlebars'],
            ],

            hbsShortComment: [
                [/\}\}/, 'comment.handlebars', '@pop'],
                [/./, 'comment.handlebars'],
            ],

            hbsRawExpression: [
                [/\}\}\}/, 'delimiter.handlebars', '@pop'],
                [/[\w.]+/, 'raw.handlebars'],
                [/"[^"]*"/, 'string.handlebars'],
                [/'[^']*'/, 'string.handlebars'],
                [/\s+/, ''],
            ],

            hbsBlockOpen: [
                [/\}\}/, 'delimiter.handlebars', '@pop'],
                [/(?:if|unless|each|with|ifEquals|lookup|log)\b/, 'keyword.helper.handlebars'],
                [/[\w.]+/, 'variable.handlebars'],
                [/"[^"]*"/, 'string.handlebars'],
                [/'[^']*'/, 'string.handlebars'],
                [/\s+/, ''],
            ],

            hbsBlockClose: [
                [/\}\}/, 'delimiter.handlebars', '@pop'],
                [/(?:if|unless|each|with|ifEquals|lookup|log)\b/, 'keyword.helper.handlebars'],
                [/[\w.]+/, 'variable.handlebars'],
                [/\s+/, ''],
            ],

            hbsExpression: [
                [/\}\}/, 'delimiter.handlebars', '@pop'],
                [/(?:upper|lower|formatDate|currency|qrCode|barCode)\b/, 'keyword.helper.handlebars'],
                [/(?:else)\b/, 'keyword.handlebars'],
                [/[\w.]+/, 'variable.handlebars'],
                [/"[^"]*"/, 'string.handlebars'],
                [/'[^']*'/, 'string.handlebars'],
                [/\s+/, ''],
            ],

            // ── HTML States ──
            htmlComment: [
                [/-->/, 'comment.html', '@pop'],
                [/./, 'comment.html'],
            ],

            doctype: [
                [/>/, 'metatag.html', '@pop'],
                [/./, 'metatag.content.html'],
            ],

            htmlTag: [
                // Handlebars inside HTML attributes
                [/\{\{!--/, 'comment.handlebars', '@hbsComment'],
                [/\{\{\{/, 'delimiter.handlebars', '@hbsRawExpression'],
                [/\{\{\//, 'delimiter.handlebars', '@hbsBlockClose'],
                [/\{\{#/, 'delimiter.handlebars', '@hbsBlockOpen'],
                [/\{\{/, 'delimiter.handlebars', '@hbsExpression'],
                [/\/>/, 'delimiter.html', '@pop'],
                [/>/, 'delimiter.html', '@pop'],
                [/"([^"]*)"/, 'attribute.value.html'],
                [/'([^']*)'/, 'attribute.value.html'],
                [/=/, 'delimiter.html'],
                [/[\w\-]+/, 'attribute.name.html'],
                [/\s+/, ''],
            ],

            htmlEndTag: [
                [/>/, 'delimiter.html', '@pop'],
                [/\s+/, ''],
            ],

            // Script and style blocks (simplified)
            script: [
                [/<\/script\s*>/, { token: '@rematch', next: '@pop' }],
                [/./, ''],
            ],

            style: [
                [/<\/style\s*>/, { token: '@rematch', next: '@pop' }],
                [/./, ''],
            ],
        }
    });

    // Configure bracket matching and autocompletion for handlebars-html
    monaco.languages.setLanguageConfiguration('handlebars-html', {
        comments: {
            blockComment: ['{{!--', '--}}']
        },
        brackets: [
            ['{', '}'],
            ['[', ']'],
            ['(', ')'],
            ['<', '>']
        ],
        autoClosingPairs: [
            { open: '{', close: '}' },
            { open: '[', close: ']' },
            { open: '(', close: ')' },
            { open: '<', close: '>' },
            { open: '"', close: '"' },
            { open: "'", close: "'" },
            { open: '{{', close: '}}' },
            { open: '<!--', close: '-->' }
        ],
        surroundingPairs: [
            { open: '{', close: '}' },
            { open: '[', close: ']' },
            { open: '(', close: ')' },
            { open: '<', close: '>' },
            { open: '"', close: '"' },
            { open: "'", close: "'" }
        ],
        indentationRules: {
            increaseIndentPattern: /<(?!(?:area|base|br|col|embed|hr|img|input|keygen|link|menuitem|meta|param|source|track|wbr)\b)[a-zA-Z][\w-]*[^\/]*>(?!\s*<\/)|{{#/,
            decreaseIndentPattern: /<\/[a-zA-Z][\w-]*>|{{\/|--}}/
        },
        onEnterRules: [
            {
                beforeText: /<(?!(?:area|base|br|col|embed|hr|img|input|keygen|link|menuitem|meta|param|source|track|wbr)\b)[a-zA-Z][\w-]*[^\/]*>$/,
                afterText: /^<\//,
                action: { indentAction: monaco.languages.IndentAction.IndentOutdent }
            },
            {
                beforeText: /{{#\w+.*}}$/,
                afterText: /^{{\//,
                action: { indentAction: monaco.languages.IndentAction.IndentOutdent }
            }
        ]
    });

    // ── Register handlebars-css language ──
    monaco.languages.register({
        id: 'handlebars-css',
        extensions: [],
        aliases: ['Handlebars CSS', 'hbs-css'],
        mimetypes: ['text/x-handlebars-css']
    });

    monaco.languages.setMonarchTokensProvider('handlebars-css', {
        defaultToken: '',
        tokenPostfix: '.css',

        tokenizer: {
            root: [
                // Handlebars expressions within CSS
                [/\{\{!--/, 'comment.handlebars', '@hbsComment'],
                [/\{\{\{/, 'delimiter.handlebars', '@hbsRawExpression'],
                [/\{\{/, 'delimiter.handlebars', '@hbsExpression'],
                // CSS comments
                [/\/\*/, 'comment.css', '@cssComment'],
                [/\/\/.*$/, 'comment.css'],
                // Selectors & at-rules
                [/@[\w-]+/, 'keyword.css'],
                // Numbers with units
                [/(-?\d*\.\d+)(px|em|rem|%|pt|mm|cm|in|vh|vw|vmin|vmax|deg|rad|s|ms)?/, ['number.css', 'attribute.value.unit.css']],
                [/-?\d+(\.\d+)?(px|em|rem|%|pt|mm|cm|in|vh|vw|vmin|vmax|deg|rad|s|ms)?/, ['number.css', 'attribute.value.unit.css']],
                // Hex colors
                [/#[0-9a-fA-F]{3,8}\b/, 'attribute.value.hex.css'],
                // Strings
                [/"[^"]*"/, 'string.css'],
                [/'[^']*'/, 'string.css'],
                // Property names (before colon)
                [/[\w-]+(?=\s*:)/, 'attribute.name.css'],
                // Selectors
                [/[.#][\w-]+/, 'tag.css'],
                [/[\w-]+(?=\s*[{,])/, 'tag.css'],
                // Punctuation
                [/[{}]/, 'delimiter.css'],
                [/[;:]/, 'delimiter.css'],
                [/[()]/, 'delimiter.parenthesis.css'],
                // CSS values and keywords
                [/!important\b/, 'keyword.css'],
                [/\b(?:inherit|initial|unset|none|auto|normal|bold|italic|block|inline|flex|grid|absolute|relative|fixed|sticky|hidden|visible|solid|dashed|dotted|transparent|currentColor)\b/, 'attribute.value.css'],
                // Other identifiers
                [/[\w-]+/, 'attribute.value.css'],
                [/\s+/, ''],
            ],

            hbsComment: [
                [/--\}\}/, 'comment.handlebars', '@pop'],
                [/./, 'comment.handlebars'],
            ],

            hbsRawExpression: [
                [/\}\}\}/, 'delimiter.handlebars', '@pop'],
                [/[\w.]+/, 'raw.handlebars'],
                [/"[^"]*"/, 'string.handlebars'],
                [/'[^']*'/, 'string.handlebars'],
                [/\s+/, ''],
            ],

            hbsExpression: [
                [/\}\}/, 'delimiter.handlebars', '@pop'],
                [/(?:upper|lower|formatDate|currency)\b/, 'keyword.helper.handlebars'],
                [/[\w.]+/, 'variable.handlebars'],
                [/"[^"]*"/, 'string.handlebars'],
                [/'[^']*'/, 'string.handlebars'],
                [/\s+/, ''],
            ],

            cssComment: [
                [/\*\//, 'comment.css', '@pop'],
                [/./, 'comment.css'],
            ],
        }
    });

    monaco.languages.setLanguageConfiguration('handlebars-css', {
        comments: {
            blockComment: ['/*', '*/']
        },
        brackets: [
            ['{', '}'],
            ['[', ']'],
            ['(', ')']
        ],
        autoClosingPairs: [
            { open: '{', close: '}' },
            { open: '[', close: ']' },
            { open: '(', close: ')' },
            { open: '"', close: '"' },
            { open: "'", close: "'" },
            { open: '{{', close: '}}' },
            { open: '/*', close: '*/' }
        ],
        surroundingPairs: [
            { open: '{', close: '}' },
            { open: '[', close: ']' },
            { open: '(', close: ')' },
            { open: '"', close: '"' },
            { open: "'", close: "'" }
        ]
    });
}

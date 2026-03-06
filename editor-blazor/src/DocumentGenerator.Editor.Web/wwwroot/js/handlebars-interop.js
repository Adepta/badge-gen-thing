// Handlebars Interop for Blazor
// Compiles Handlebars templates, registers custom helpers, builds preview HTML.
// Expects Handlebars to be loaded globally via <script> tag before this module is imported.

/**
 * Wait for Handlebars to be available (loaded via script tag in App.razor).
 */
function getHandlebars() {
    if (typeof Handlebars !== 'undefined') {
        return Handlebars;
    }
    throw new Error('Handlebars is not loaded. Ensure handlebars.min.js is included via a <script> tag.');
}

/**
 * Register all custom helpers matching the existing editor.
 */
export function registerHelpers() {
    const hbs = getHandlebars();

    // qrCode helper - returns an SVG placeholder for QR codes
    hbs.registerHelper('qrCode', function (value) {
        const text = typeof value === 'string' ? value : (value || '');
        return new hbs.SafeString(
            `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 80 80" width="80" height="80" style="display:inline-block;">` +
            `<rect width="80" height="80" fill="#fff" stroke="#333" stroke-width="2" rx="4"/>` +
            `<rect x="8" y="8" width="24" height="24" fill="#333" rx="2"/>` +
            `<rect x="48" y="8" width="24" height="24" fill="#333" rx="2"/>` +
            `<rect x="8" y="48" width="24" height="24" fill="#333" rx="2"/>` +
            `<rect x="14" y="14" width="12" height="12" fill="#fff" rx="1"/>` +
            `<rect x="54" y="14" width="12" height="12" fill="#fff" rx="1"/>` +
            `<rect x="14" y="54" width="12" height="12" fill="#fff" rx="1"/>` +
            `<rect x="18" y="18" width="4" height="4" fill="#333"/>` +
            `<rect x="58" y="18" width="4" height="4" fill="#333"/>` +
            `<rect x="18" y="58" width="4" height="4" fill="#333"/>` +
            `<rect x="36" y="36" width="8" height="8" fill="#333"/>` +
            `<rect x="48" y="48" width="8" height="8" fill="#333"/>` +
            `<rect x="60" y="48" width="8" height="8" fill="#333"/>` +
            `<rect x="48" y="60" width="8" height="8" fill="#333"/>` +
            `<rect x="60" y="60" width="8" height="8" fill="#333"/>` +
            `</svg>`
        );
    });

    // barCode helper - returns an SVG placeholder for barcodes
    hbs.registerHelper('barCode', function (value) {
        const text = typeof value === 'string' ? value : (value || '');
        let bars = '';
        const widths = [2, 1, 3, 1, 2, 1, 1, 3, 2, 1, 3, 1, 2, 1, 1, 2, 3, 1, 2, 1];
        let x = 4;
        for (let i = 0; i < widths.length; i++) {
            bars += `<rect x="${x}" y="4" width="${widths[i]}" height="32" fill="#333"/>`;
            x += widths[i] + 1;
        }
        return new hbs.SafeString(
            `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 ${x + 4} 44" width="${x + 4}" height="44" style="display:inline-block;">` +
            `<rect width="${x + 4}" height="44" fill="#fff" stroke="#333" stroke-width="1" rx="2"/>` +
            bars +
            `</svg>`
        );
    });

    // upper helper - converts text to uppercase
    hbs.registerHelper('upper', function (value) {
        return typeof value === 'string' ? value.toUpperCase() : '';
    });

    // lower helper - converts text to lowercase
    hbs.registerHelper('lower', function (value) {
        return typeof value === 'string' ? value.toLowerCase() : '';
    });

    // formatDate helper - formats a date string
    hbs.registerHelper('formatDate', function (value, format) {
        if (!value) return '';
        try {
            const date = new Date(value);
            if (isNaN(date.getTime())) return value;

            const formatStr = typeof format === 'string' ? format : 'en-GB';
            return date.toLocaleDateString(formatStr, {
                year: 'numeric',
                month: 'long',
                day: 'numeric'
            });
        } catch {
            return value;
        }
    });

    // currency helper - formats a number as currency
    hbs.registerHelper('currency', function (value, currencyCode) {
        const num = parseFloat(value);
        if (isNaN(num)) return value || '';

        const code = typeof currencyCode === 'string' ? currencyCode : 'GBP';
        try {
            return new Intl.NumberFormat('en-GB', {
                style: 'currency',
                currency: code
            }).format(num);
        } catch {
            return `${code} ${num.toFixed(2)}`;
        }
    });

    // ifEquals block helper - compares two values, renders block if equal
    hbs.registerHelper('ifEquals', function (a, b, options) {
        if (a === b) {
            return options.fn(this);
        }
        return options.inverse(this);
    });
}

/**
 * Compile a Handlebars HTML template with data.
 * @param {string} htmlTemplate - The Handlebars HTML template string.
 * @param {object} data - The data context for rendering.
 * @returns {string} The rendered HTML string.
 */
export function compile(htmlTemplate, data) {
    if (!htmlTemplate) return '';

    const hbs = getHandlebars();
    try {
        const template = hbs.compile(htmlTemplate, { noEscape: false });
        return template(data || {});
    } catch (e) {
        console.error('Handlebars compilation error:', e);
        return `<div style="color:red;padding:12px;font-family:monospace;font-size:12px;">
            <strong>Template Error:</strong><br/>${escapeHtml(e.message)}
        </div>`;
    }
}

/**
 * Resolve CSS tokens (e.g., {{branding.primaryColour}}) in CSS content.
 * Uses simple regex replacement since CSS values aren't valid Handlebars.
 * @param {string} cssContent - The CSS content with Handlebars tokens.
 * @param {object} data - The data context for token resolution.
 * @returns {string} CSS with tokens replaced by data values.
 */
export function resolveCssTokens(cssContent, data) {
    if (!cssContent) return '';
    if (!data) return cssContent;

    return cssContent.replace(/\{\{([^}]+)\}\}/g, function (match, key) {
        const trimmedKey = key.trim();
        const value = resolveDataPath(data, trimmedKey);
        return value !== undefined ? value : match;
    });
}

/**
 * Build a complete preview HTML document with <style> block.
 * @param {string} html - The HTML template content.
 * @param {string} css - The CSS content.
 * @param {object} data - The data context.
 * @param {string} mode - "editor" or "live".
 * @returns {string} A complete HTML document ready for iframe rendering.
 */
export function buildPreviewHtml(html, css, data, mode) {
    let resolvedCss = resolveCssTokens(css, data);
    let resolvedHtml;

    if (mode === 'live') {
        // Full Handlebars compilation with data
        resolvedHtml = compile(html, data);
    } else {
        // Editor mode: leave double-stache tokens as-is, but resolve triple-stache helpers (qrCode, barCode)
        resolvedHtml = resolveTripleStacheHelpers(html, data);
    }

    return `<!DOCTYPE html>
<html>
<head>
    <meta charset="utf-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <style>
        *, *::before, *::after { box-sizing: border-box; margin: 0; padding: 0; }
        body { font-family: 'Segoe UI', Arial, sans-serif; }
    </style>
    <style>
${resolvedCss}
    </style>
</head>
<body>
${resolvedHtml}
</body>
</html>`;
}

/**
 * Resolve triple-stache helpers like {{{qrCode ...}}} and {{{barCode ...}}}
 * while leaving double-stache tokens intact for editor mode.
 */
function resolveTripleStacheHelpers(html, data) {
    if (!html) return '';

    const hbs = getHandlebars();

    // Replace triple-stache helper calls with their SVG output
    return html.replace(/\{\{\{(qrCode|barCode)\s+([^}]*)\}\}\}/g, function (match, helper, arg) {
        try {
            const trimmedArg = arg.trim();
            // Resolve the argument from data if it's a path
            let value = resolveDataPath(data, trimmedArg) || trimmedArg;
            const helperFn = hbs.helpers[helper];
            if (helperFn) {
                const result = helperFn(value);
                return result && result.string ? result.string : String(result || '');
            }
        } catch (e) {
            console.warn(`Error resolving helper ${helper}:`, e);
        }
        return match;
    });
}

/**
 * Resolve a dotted data path (e.g., "branding.primaryColour") from a nested object.
 */
function resolveDataPath(data, path) {
    if (!data || !path) return undefined;

    const parts = path.split('.');
    let current = data;

    for (const part of parts) {
        if (current === null || current === undefined || typeof current !== 'object') {
            return undefined;
        }
        current = current[part];
    }

    return current;
}

/**
 * Escape HTML entities for safe display in error messages.
 */
function escapeHtml(str) {
    if (!str) return '';
    return str.replace(/&/g, '&amp;')
              .replace(/</g, '&lt;')
              .replace(/>/g, '&gt;')
              .replace(/"/g, '&quot;');
}

// Monaco Editor Color Provider
// Adds inline color decorators and a color picker for:
// - Hex colors (#fff, #ffffff, #ffffffff)
// - rgb()/rgba() functions
// - hsl()/hsla() functions
// - Named CSS colors
// - Handlebars branding color tokens (resolved from sample data)

// Current sample data color values, updated from C#
let _brandingColors = {
    'branding.primaryColour': '#6C3CE1',
    'branding.secondaryColour': '#F3F0FF',
    'branding.custom.accentColour': '#FF5A5F',
};

// Common named CSS colors for detection
const NAMED_COLORS = {
    'aliceblue': [0.94, 0.97, 1, 1], 'antiquewhite': [0.98, 0.92, 0.84, 1],
    'aqua': [0, 1, 1, 1], 'aquamarine': [0.50, 1, 0.83, 1],
    'black': [0, 0, 0, 1], 'blue': [0, 0, 1, 1],
    'brown': [0.65, 0.16, 0.16, 1], 'coral': [1, 0.50, 0.31, 1],
    'crimson': [0.86, 0.08, 0.24, 1], 'cyan': [0, 1, 1, 1],
    'darkblue': [0, 0, 0.55, 1], 'darkgray': [0.66, 0.66, 0.66, 1],
    'darkgreen': [0, 0.39, 0, 1], 'darkred': [0.55, 0, 0, 1],
    'deeppink': [1, 0.08, 0.58, 1], 'dodgerblue': [0.12, 0.56, 1, 1],
    'gold': [1, 0.84, 0, 1], 'gray': [0.50, 0.50, 0.50, 1],
    'green': [0, 0.50, 0, 1], 'grey': [0.50, 0.50, 0.50, 1],
    'hotpink': [1, 0.41, 0.71, 1], 'indianred': [0.80, 0.36, 0.36, 1],
    'indigo': [0.29, 0, 0.51, 1], 'ivory': [1, 1, 0.94, 1],
    'lavender': [0.90, 0.90, 0.98, 1], 'limegreen': [0.20, 0.80, 0.20, 1],
    'magenta': [1, 0, 1, 1], 'maroon': [0.50, 0, 0, 1],
    'navy': [0, 0, 0.50, 1], 'olive': [0.50, 0.50, 0, 1],
    'orange': [1, 0.65, 0, 1], 'orangered': [1, 0.27, 0, 1],
    'orchid': [0.85, 0.44, 0.84, 1], 'pink': [1, 0.75, 0.80, 1],
    'plum': [0.87, 0.63, 0.87, 1], 'purple': [0.50, 0, 0.50, 1],
    'rebeccapurple': [0.40, 0.20, 0.60, 1], 'red': [1, 0, 0, 1],
    'royalblue': [0.25, 0.41, 0.88, 1], 'salmon': [0.98, 0.50, 0.45, 1],
    'seagreen': [0.18, 0.55, 0.34, 1], 'silver': [0.75, 0.75, 0.75, 1],
    'skyblue': [0.53, 0.81, 0.92, 1], 'slategray': [0.44, 0.50, 0.56, 1],
    'steelblue': [0.27, 0.51, 0.71, 1], 'teal': [0, 0.50, 0.50, 1],
    'tomato': [1, 0.39, 0.28, 1], 'turquoise': [0.25, 0.88, 0.82, 1],
    'violet': [0.93, 0.51, 0.93, 1], 'wheat': [0.96, 0.87, 0.70, 1],
    'white': [1, 1, 1, 1], 'whitesmoke': [0.96, 0.96, 0.96, 1],
    'yellow': [1, 1, 0, 1], 'yellowgreen': [0.60, 0.80, 0.20, 1],
    'transparent': [0, 0, 0, 0],
};

/**
 * Parse a hex color string to [r, g, b, a] in 0-1 range.
 */
function parseHex(hex) {
    hex = hex.replace('#', '');
    let r, g, b, a = 1;
    if (hex.length === 3) {
        r = parseInt(hex[0] + hex[0], 16) / 255;
        g = parseInt(hex[1] + hex[1], 16) / 255;
        b = parseInt(hex[2] + hex[2], 16) / 255;
    } else if (hex.length === 4) {
        r = parseInt(hex[0] + hex[0], 16) / 255;
        g = parseInt(hex[1] + hex[1], 16) / 255;
        b = parseInt(hex[2] + hex[2], 16) / 255;
        a = parseInt(hex[3] + hex[3], 16) / 255;
    } else if (hex.length === 6) {
        r = parseInt(hex.substring(0, 2), 16) / 255;
        g = parseInt(hex.substring(2, 4), 16) / 255;
        b = parseInt(hex.substring(4, 6), 16) / 255;
    } else if (hex.length === 8) {
        r = parseInt(hex.substring(0, 2), 16) / 255;
        g = parseInt(hex.substring(2, 4), 16) / 255;
        b = parseInt(hex.substring(4, 6), 16) / 255;
        a = parseInt(hex.substring(6, 8), 16) / 255;
    } else {
        return null;
    }
    if (isNaN(r) || isNaN(g) || isNaN(b) || isNaN(a)) return null;
    return [r, g, b, a];
}

/**
 * Convert [r, g, b, a] (0-1 range) back to a hex string.
 */
function toHex(r, g, b, a) {
    const rr = Math.round(r * 255).toString(16).padStart(2, '0');
    const gg = Math.round(g * 255).toString(16).padStart(2, '0');
    const bb = Math.round(b * 255).toString(16).padStart(2, '0');
    if (a !== undefined && a < 1) {
        const aa = Math.round(a * 255).toString(16).padStart(2, '0');
        return `#${rr}${gg}${bb}${aa}`;
    }
    return `#${rr}${gg}${bb}`;
}

/**
 * Parse rgb()/rgba() to [r, g, b, a] in 0-1 range.
 */
function parseRgb(str) {
    const match = str.match(/rgba?\(\s*([\d.]+%?)\s*[,\s]\s*([\d.]+%?)\s*[,\s]\s*([\d.]+%?)\s*(?:[,/]\s*([\d.]+%?))?\s*\)/i);
    if (!match) return null;
    const parse = (v) => v.endsWith('%') ? parseFloat(v) / 100 : parseFloat(v) / 255;
    const r = parse(match[1]);
    const g = parse(match[2]);
    const b = parse(match[3]);
    const a = match[4] ? (match[4].endsWith('%') ? parseFloat(match[4]) / 100 : parseFloat(match[4])) : 1;
    return [r, g, b, a];
}

/**
 * Convert [r, g, b, a] to rgb()/rgba() string.
 */
function toRgb(r, g, b, a) {
    const rr = Math.round(r * 255);
    const gg = Math.round(g * 255);
    const bb = Math.round(b * 255);
    if (a < 1) {
        return `rgba(${rr}, ${gg}, ${bb}, ${parseFloat(a.toFixed(2))})`;
    }
    return `rgb(${rr}, ${gg}, ${bb})`;
}

/**
 * Parse hsl()/hsla() to [r, g, b, a] in 0-1 range.
 */
function parseHsl(str) {
    const match = str.match(/hsla?\(\s*([\d.]+)\s*[,\s]\s*([\d.]+)%\s*[,\s]\s*([\d.]+)%\s*(?:[,/]\s*([\d.]+%?))?\s*\)/i);
    if (!match) return null;
    const h = parseFloat(match[1]) / 360;
    const s = parseFloat(match[2]) / 100;
    const l = parseFloat(match[3]) / 100;
    const a = match[4] ? (match[4].endsWith('%') ? parseFloat(match[4]) / 100 : parseFloat(match[4])) : 1;

    // HSL to RGB
    let r, g, b;
    if (s === 0) {
        r = g = b = l;
    } else {
        const hue2rgb = (p, q, t) => {
            if (t < 0) t += 1;
            if (t > 1) t -= 1;
            if (t < 1/6) return p + (q - p) * 6 * t;
            if (t < 1/2) return q;
            if (t < 2/3) return p + (q - p) * (2/3 - t) * 6;
            return p;
        };
        const q = l < 0.5 ? l * (1 + s) : l + s - l * s;
        const p = 2 * l - q;
        r = hue2rgb(p, q, h + 1/3);
        g = hue2rgb(p, q, h);
        b = hue2rgb(p, q, h - 1/3);
    }
    return [r, g, b, a];
}

/**
 * Find all color values in a line of text.
 * Returns array of { match, startCol, endCol, color: [r,g,b,a] }
 */
function findColorsInLine(lineContent, lineNumber) {
    const results = [];

    // Hex colors: #rgb, #rgba, #rrggbb, #rrggbbaa
    const hexRegex = /#(?:[0-9a-fA-F]{3,4}|[0-9a-fA-F]{6}|[0-9a-fA-F]{8})\b/g;
    let m;
    while ((m = hexRegex.exec(lineContent)) !== null) {
        const color = parseHex(m[0]);
        if (color) {
            results.push({
                match: m[0],
                range: { startLineNumber: lineNumber, startColumn: m.index + 1, endLineNumber: lineNumber, endColumn: m.index + m[0].length + 1 },
                color: { red: color[0], green: color[1], blue: color[2], alpha: color[3] }
            });
        }
    }

    // rgb() / rgba()
    const rgbRegex = /rgba?\(\s*[\d.]+%?\s*[,\s]\s*[\d.]+%?\s*[,\s]\s*[\d.]+%?\s*(?:[,/]\s*[\d.]+%?)?\s*\)/gi;
    while ((m = rgbRegex.exec(lineContent)) !== null) {
        const color = parseRgb(m[0]);
        if (color) {
            results.push({
                match: m[0],
                range: { startLineNumber: lineNumber, startColumn: m.index + 1, endLineNumber: lineNumber, endColumn: m.index + m[0].length + 1 },
                color: { red: color[0], green: color[1], blue: color[2], alpha: color[3] }
            });
        }
    }

    // hsl() / hsla()
    const hslRegex = /hsla?\(\s*[\d.]+\s*[,\s]\s*[\d.]+%\s*[,\s]\s*[\d.]+%\s*(?:[,/]\s*[\d.]+%?)?\s*\)/gi;
    while ((m = hslRegex.exec(lineContent)) !== null) {
        const color = parseHsl(m[0]);
        if (color) {
            results.push({
                match: m[0],
                range: { startLineNumber: lineNumber, startColumn: m.index + 1, endLineNumber: lineNumber, endColumn: m.index + m[0].length + 1 },
                color: { red: color[0], green: color[1], blue: color[2], alpha: color[3] }
            });
        }
    }

    // Named CSS colors (only in CSS contexts - look for property: value patterns)
    const namedRegex = /\b([a-zA-Z]+)\b/g;
    while ((m = namedRegex.exec(lineContent)) !== null) {
        const name = m[1].toLowerCase();
        if (NAMED_COLORS[name]) {
            // Only match named colors that appear after a colon (CSS property context)
            const before = lineContent.substring(0, m.index);
            if (before.includes(':')) {
                const c = NAMED_COLORS[name];
                results.push({
                    match: m[0],
                    range: { startLineNumber: lineNumber, startColumn: m.index + 1, endLineNumber: lineNumber, endColumn: m.index + m[0].length + 1 },
                    color: { red: c[0], green: c[1], blue: c[2], alpha: c[3] }
                });
            }
        }
    }

    // Handlebars branding color tokens: {{branding.primaryColour}} etc.
    const hbsRegex = /\{\{(branding\.(?:primaryColour|secondaryColour|custom\.accentColour))\}\}/g;
    while ((m = hbsRegex.exec(lineContent)) !== null) {
        const tokenKey = m[1];
        const hexValue = _brandingColors[tokenKey];
        if (hexValue) {
            const color = parseHex(hexValue);
            if (color) {
                results.push({
                    match: m[0],
                    range: { startLineNumber: lineNumber, startColumn: m.index + 1, endLineNumber: lineNumber, endColumn: m.index + m[0].length + 1 },
                    color: { red: color[0], green: color[1], blue: color[2], alpha: color[3] },
                    isBrandingToken: true,
                    tokenKey: tokenKey
                });
            }
        }
    }

    return results;
}

/**
 * Register color providers for handlebars-html and handlebars-css.
 */
export function registerColorProviders() {
    const colorProvider = {
        provideDocumentColors(model) {
            const colors = [];
            const lineCount = model.getLineCount();

            for (let i = 1; i <= lineCount; i++) {
                const line = model.getLineContent(i);
                const found = findColorsInLine(line, i);
                for (const item of found) {
                    colors.push({
                        range: item.range,
                        color: item.color
                    });
                }
            }

            return colors;
        },

        provideColorPresentations(model, colorInfo) {
            const { red, green, blue, alpha } = colorInfo.color;
            const presentations = [];

            // Get the original text to determine what format to use
            const originalText = model.getValueInRange(colorInfo.range);

            // Check if this is a Handlebars token
            if (originalText.startsWith('{{')) {
                // For Handlebars tokens, replace with the actual hex value
                const hex = toHex(red, green, blue, alpha);
                presentations.push({
                    label: `${hex} (replaces token)`,
                    textEdit: { range: colorInfo.range, text: hex }
                });
                // Also offer keeping the token but updating the sample data
                presentations.push({
                    label: originalText + ' (keep token)',
                    textEdit: { range: colorInfo.range, text: originalText }
                });
                return presentations;
            }

            // Hex presentation
            const hex = toHex(red, green, blue, alpha);
            presentations.push({
                label: hex,
                textEdit: { range: colorInfo.range, text: hex }
            });

            // RGB presentation
            const rgb = toRgb(red, green, blue, alpha);
            presentations.push({
                label: rgb,
                textEdit: { range: colorInfo.range, text: rgb }
            });

            // If the original was a named color, include hex as primary
            return presentations;
        }
    };

    // Register for both languages
    monaco.languages.registerColorProvider('handlebars-html', colorProvider);
    monaco.languages.registerColorProvider('handlebars-css', colorProvider);

    // Also register for plain css/html in case those are used
    monaco.languages.registerColorProvider('css', colorProvider);
    monaco.languages.registerColorProvider('html', colorProvider);
}

/**
 * Update the branding color values from sample data.
 * Called from C# when sample data changes.
 * @param {Object} colors - Map of token key to hex value, e.g. { 'branding.primaryColour': '#6C3CE1' }
 */
export function updateBrandingColors(colors) {
    if (colors && typeof colors === 'object') {
        _brandingColors = { ..._brandingColors, ...colors };
    }
}

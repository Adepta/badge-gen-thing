# Template Schema

Every document is defined by a single JSON file conforming to the `DocumentTemplate` schema.

## Top-level structure

```json
{
  "documentType": "badge",
  "version": "1.0",
  "branding": { ... },
  "template": { ... },
  "variables": { ... },
  "pdf": { ... }
}
```

| Field | Type | Description |
|---|---|---|
| `documentType` | `string` | Logical identifier e.g. `"badge"`, `"invoice"` |
| `version` | `string` | Template schema version, defaults to `"1.0"` |
| `branding` | `Branding` | Company branding applied to every render |
| `template` | `TemplateContent` | Handlebars HTML and CSS |
| `variables` | `object` | Freeform key/value data injected into the template |
| `pdf` | `PdfOptions` | Paper size, margins, orientation |

## Branding

```json
"branding": {
  "companyName": "TechConf 2026",
  "logoUrl": "https://example.com/logo.png",
  "primaryColour": "#6C3CE1",
  "secondaryColour": "#F3F0FF",
  "headingFont": "'Segoe UI', Arial, sans-serif",
  "bodyFont": "'Segoe UI', Arial, sans-serif",
  "custom": {
    "anyKey": "anyValue"
  }
}
```

Branding values are available in templates as `{{branding.companyName}}`, `{{branding.primaryColour}}`, `{{branding.custom.anyKey}}` etc.

## Template content

Templates support two styles — file-based (recommended) and inline. Both can be mixed within the same JSON file; `htmlPath`/`cssPath` take precedence over inline content when both are set.

### File-based (recommended)

```json
"template": {
  "htmlPath": "badge.html",
  "cssPath":  "badge.css",
  "partials": {}
}
```

Paths are resolved **relative to the directory that contains the JSON file**. Absolute paths are also accepted. The files are loaded at render time by `FileTemplateContentResolver`.

### Inline

```json
"template": {
  "html": "<!DOCTYPE html><html>...",
  "css": "body { font-family: {{branding.bodyFont}}; }",
  "partials": {
    "header": "<header>{{branding.companyName}}</header>"
  }
}
```

Inline content is still fully supported — useful for Kafka payloads where the template is embedded in the message.

Both `html`/`htmlPath` and `css`/`cssPath` are Handlebars templates themselves. The CSS is automatically injected into a `<style>` tag before the closing `</head>`.

## Handlebars context

The full context available to every template:

```
{{branding.companyName}}
{{branding.logoUrl}}
{{branding.primaryColour}}
{{branding.secondaryColour}}
{{branding.headingFont}}
{{branding.bodyFont}}
{{branding.custom.<key>}}

{{variables.<key>}}
{{variables.<nested>.<key>}}

{{meta.documentType}}
{{meta.version}}
{{meta.generatedAt}}
```

## Built-in helpers

| Helper | Example | Output |
|---|---|---|
| `upper` | `{{upper variables.firstName}}` | `JANE` |
| `lower` | `{{lower variables.ticketType}}` | `speaker` |
| `formatDate` | `{{formatDate variables.date "dd MMM yyyy"}}` | `24 Feb 2026` |
| `currency` | `{{currency variables.amount "en-GB"}}` | `£1,500.00` |
| `ifEquals` | `{{#ifEquals variables.role "admin"}}...{{/ifEquals}}` | Conditional block |
| `qrCode` | `{{{qrCode variables.attendeeId "#ffffff" "transparent"}}}` | Inline SVG QR code |
| `barCode` | `{{{barCode variables.attendeeId "40" "false" "#A3E635"}}}` | Inline SVG Code-128 barcode |

> **Note:** `qrCode` and `barCode` emit raw SVG markup — use triple-stache `{{{ }}}` to prevent HTML escaping.

See [Handlebars Helpers](handlebars-helpers.md) for full signatures and examples.

## PDF options

```json
"pdf": {
  "format": "A4",
  "landscape": false,
  "printBackground": true,
  "scale": 1.0,
  "margins": {
    "top": "15mm",
    "bottom": "15mm",
    "left": "15mm",
    "right": "15mm"
  },
  "headerTemplate": "<div>...</div>",
  "footerTemplate": "<div>Page <span class='pageNumber'></span></div>"
}
```

Supported formats: `A2`, `A3`, `A4`, `Letter`, `Legal`, `Tabloid`.

# Handlebars Helpers

DocumentGenerator registers a set of built-in helpers on the Handlebars engine at startup. All helpers are available in both `html` and `css` template fields.

> **SVG helpers (`qrCode`, `barCode`)** emit raw markup — always use triple-stache `{{{ }}}` to prevent HTML-escaping the angle brackets.

---

## `upper`

Converts a string to uppercase.

**Signature:** `{{upper value}}`

```handlebars
{{upper variables.firstName}}
{{upper variables.ticketType}}
```

```
JANE
SPEAKER
```

---

## `lower`

Converts a string to lowercase.

**Signature:** `{{lower value}}`

```handlebars
{{lower variables.ticketType}}
<div class="chip chip--{{lower variables.ticketType}}">...</div>
```

```
speaker
<div class="chip chip--speaker">...</div>
```

---

## `formatDate`

Formats a date/time string using a [.NET custom date format](https://learn.microsoft.com/en-us/dotnet/standard/base-types/custom-date-and-time-format-strings).

**Signature:** `{{formatDate value "format"}}`

| Parameter | Type | Description |
|---|---|---|
| `value` | string | ISO 8601 date string, e.g. `"2026-03-15T00:00:00Z"` |
| `format` | string | .NET format pattern, e.g. `"dd MMM yyyy"` |

```handlebars
{{formatDate variables.issueDate "dd MMM yyyy"}}
{{formatDate variables.issueDate "MMMM d, yyyy"}}
{{formatDate meta.generatedAt "yyyy-MM-dd HH:mm"}}
```

```
15 Mar 2026
March 15, 2026
2026-03-15 09:42
```

Returns an empty string if the value cannot be parsed as a date.

---

## `currency`

Formats a decimal number as a localised currency string.

**Signature:** `{{currency value "culture"}}`

| Parameter | Type | Default | Description |
|---|---|---|---|
| `value` | string | — | Decimal number as a string, e.g. `"1500.00"` |
| `culture` | string | `"en-GB"` | .NET culture name |

```handlebars
{{currency variables.total "en-GB"}}
{{currency variables.total "en-US"}}
{{currency variables.total "de-DE"}}
```

```
£1,500.00
$1,500.00
1.500,00 €
```

Returns an empty string if `value` cannot be parsed as a decimal.

---

## `ifEquals`

Block helper — renders the template block when two values are equal, the inverse block otherwise.

**Signature:** `{{#ifEquals a b}}...{{else}}...{{/ifEquals}}`

```handlebars
{{#ifEquals variables.ticketType "Speaker"}}
  <div class="ribbon speaker">Keynote Speaker</div>
{{else}}
  <div class="ribbon attendee">Attendee</div>
{{/ifEquals}}
```

Comparison is case-sensitive string equality.

---

## `qrCode`

Generates an inline SVG QR code (Code-M error correction). Use CSS `width`/`height` on the containing element to control display size — the SVG uses a `viewBox` and scales cleanly.

**Signature:** `{{{qrCode value [darkColour] [lightColour]}}}`

| Parameter | Type | Default | Description |
|---|---|---|---|
| `value` | string | — | The data to encode (attendee ID, URL, etc.) |
| `darkColour` | string | `"#000000"` | Fill colour for dark modules, e.g. `"#ffffff"` or `"#D4AF37"` |
| `lightColour` | string | `"transparent"` | Fill colour for light modules; `"transparent"` removes the background |

```handlebars
<!-- White QR on dark background (most badge designs) -->
{{{qrCode variables.attendeeId "#ffffff" "transparent"}}}

<!-- Gold QR for the Executive design -->
{{{qrCode variables.attendeeId "#D4AF37" "transparent"}}}

<!-- Neon-lime QR for the Carbon design -->
{{{qrCode variables.attendeeId "#A3E635" "transparent"}}}

<!-- Standard black on white -->
{{{qrCode variables.attendeeId}}}
```

**CSS sizing example:**

```css
.qr-code {
  width: 13mm;
  height: 13mm;
}
.qr-code svg {
  width: 100%;
  height: 100%;
  display: block;
}
```

**Implementation:** uses [QRCoder](https://github.com/codebude/QRCoder) — pure C#, no GDI+ or native dependencies.

---

## `barCode`

Generates an inline SVG Code-128 barcode. Width auto-sizes to the encoded content; control height via the parameter or CSS.

**Signature:** `{{{barCode value [height] [showText] [darkColour]}}}`

| Parameter | Type | Default | Description |
|---|---|---|---|
| `value` | string | — | The data to encode |
| `height` | integer (string) | `"60"` | Bar height in pixels |
| `showText` | `"true"` / `"false"` | `"false"` | Render the human-readable text below the bars |
| `darkColour` | string | `"#000000"` | Fill colour for the bars |

```handlebars
<!-- Default black barcode, height 60 -->
{{{barCode variables.attendeeId}}}

<!-- Neon-lime bars, height 28, no text (Carbon A6 design) -->
{{{barCode variables.attendeeId "28" "false" "#A3E635"}}}

<!-- Compact strip for credit-card size, height 14 -->
{{{barCode variables.attendeeId "14" "false" "#A3E635"}}}

<!-- Show human-readable text -->
{{{barCode variables.attendeeId "40" "true"}}}
```

**CSS sizing example:**

```css
.barcode-wrap {
  width: 100%;
  height: 8mm;
  overflow: hidden;
}
.barcode-wrap svg {
  width: 100%;
  height: 100%;
  display: block;
}
```

**Implementation:** uses [ZXing.Net](https://github.com/micjahn/ZXing.Net) — pure C#, generates Code-128 as SVG with no image rendering dependency.

---

## Adding custom helpers

Register additional helpers in `HandlebarsTemplateEngine.RegisterBuiltInHelpers` (`src/DocumentGenerator.Templating/HandlebarsTemplateEngine.cs`):

```csharp
hbs.RegisterHelper("myHelper", (output, _, args) =>
{
    var value = args[0]?.ToString() ?? string.Empty;
    output.WriteSafeString(Transform(value));
});
```

For block helpers (like `ifEquals`):

```csharp
hbs.RegisterHelper("myBlock", (output, options, context, args) =>
{
    if (SomeCondition(args))
        options.Template(output, context);
    else
        options.Inverse(output, context);
});
```

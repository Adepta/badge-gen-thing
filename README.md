# Document Generator

A .NET 10 service that renders HTML/CSS templates to PDF. Kafka messages carry
render requests in and Base64-encoded PDFs out — designed around an iPad print
workflow where devices publish a `DocumentRenderRequest` and receive a
`DocumentRenderResult` reply on the same broker.

Chromium (via PuppeteerSharp) does the actual rendering. Templates are
Handlebars with a CSS companion; the engine inlines the CSS and hands the HTML
to a pooled browser instance.

---

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (for Kafka)

---

## Quick start

### 1 — Start Kafka

```bash
docker compose -f docker-compose.kafka.yml up -d
```

This brings up Zookeeper, a single Kafka broker on `localhost:9092`, and
[Kafka UI](http://localhost:8080). A one-shot init container creates the three
topics on first run:

| Topic | Retention | Purpose |
|---|---|---|
| `render.requests` | 24 h | Inbound render jobs |
| `render.results` | 1 h | Completed PDFs (Base64) |
| `render.deadletter` | 7 d | Failed messages after all retries |

Wait for the broker health-check to pass before starting the service (~30 s):

```bash
docker compose -f docker-compose.kafka.yml ps
```

### 2 — Start the render service

```bash
dotnet run --project src/DocumentGenerator.Console
```

On first run, PuppeteerSharp downloads a pinned Chromium revision (~170 MB) and
caches it locally. Subsequent starts are instant.

The service renders a live TUI — header, scrolling log panel, status bar — and
stays running until `Ctrl+C`.

### 3 — Send a render request

In a second terminal:

```bash
dotnet run --project tools/DocumentGenerator.TestProducer
```

A menu lists seven document variants (six badge designs + invoice). Select one;
the producer sends a `DocumentRenderRequest` to Kafka, waits for the reply, and
saves the PDF to `tools/DocumentGenerator.TestProducer/Generated/`.

---

## Configuration

`src/DocumentGenerator.Console/appsettings.json` covers everything:

```jsonc
{
  "Kafka": {
    "Enabled": true,             // false → file-based worker instead
    "BootstrapServers": "localhost:9092",
    "MaxConcurrentRenders": 4    // keep aligned with BrowserPool.MaxSize
  },
  "BrowserPool": {
    "MinSize": 1,
    "MaxSize": 4,                // ~150 MB RAM per instance
    "MaxRendersPerInstance": 100 // recycle after N renders to prevent leaks
  }
}
```

To run in **file mode** (no Kafka) set `Kafka:Enabled` to `false`. The service
will scan `templates/`, render every `*.json` it finds, and write PDFs to
`output/`. Two sample templates are included.

---

## Project layout

```
src/
  DocumentGenerator.Core          Models and interfaces — no dependencies
  DocumentGenerator.Templating    Handlebars engine
  DocumentGenerator.Pdf           Chromium pool + PuppeteerSharp renderer
  DocumentGenerator.Messaging     Rebus/Kafka handler + message types
  DocumentGenerator.Console       Host, TUI, appsettings.json

tools/
  DocumentGenerator.TestProducer  Interactive test client

templates/                        Badge + invoice templates (HTML/CSS + JSON)
```

---

## Template format

Templates are JSON files with this shape:

```jsonc
{
  "documentType": "badge",
  "version": "1.0",
  "branding": {
    "companyName": "...",
    "primaryColour": "#6C3CE1",
    "headingFont": "Segoe UI, Arial, sans-serif",
    "custom": { "accentColour": "#FF5A5F" }   // brand-level extras
  },
  "template": {
    // Option A — external files (recommended)
    "htmlPath": "badge.html",                  // path relative to this JSON file
    "cssPath":  "badge.css",
    // Option B — inline content (still supported; takes precedence if both set)
    // "html": "... Handlebars ...",
    // "css":  "... Handlebars + CSS ...",
    "partials": {}
  },
  "variables": {                               // per-document data
    "firstName": "Jane",
    "ticketType": "Speaker"
  },
  "pdf": {
    "width": "85.6mm", "height": "54mm",       // omit for named formats
    "printBackground": true,
    "margins": { "top": "0mm", "bottom": "0mm", "left": "0mm", "right": "0mm" }
  }
}
```

`htmlPath` and `cssPath` are resolved relative to the directory that contains
the JSON file.  Absolute paths are also accepted.  Inline `html`/`css` strings
remain supported for backwards compatibility (e.g. Kafka payloads).

The Handlebars context exposes `{{branding.*}}`, `{{variables.*}}`, and
`{{meta.generatedAt}}`. Built-in helpers: `upper`, `lower`, `formatDate`,
`currency`, `ifEquals`.

### Included badge designs

| File prefix | Size | Design |
|---|---|---|
| `badge` | Credit-card (85.6×54mm) | Original purple gradient |
| `badge-pulse-a6` | A6 (105×148mm) | Deep navy, diagonal accent stripe |
| `badge-pulse-cc` | Credit-card | Same Pulse design, compact |
| `badge-carbon-a6` | A6 | Near-black + neon-lime, terminal aesthetic |
| `badge-carbon-cc` | Credit-card | Same Carbon design, compact |
| `badge-executive-a6` | A6 | Charcoal + gold, serif luxury |
| `badge-executive-cc` | Credit-card | Same Executive design, compact |

---

## Stopping

```bash
# Render service / test producer
Ctrl+C

# Kafka stack
docker compose -f docker-compose.kafka.yml down

# Including volumes (wipes all topic data)
docker compose -f docker-compose.kafka.yml down -v
```

---

## TODO

- [x] Add unit and integration tests (`tests/` — xUnit, Moq, FluentAssertions)
- [x] Update templates to use standalone HTML and CSS files instead of inlining them in the JSON payload

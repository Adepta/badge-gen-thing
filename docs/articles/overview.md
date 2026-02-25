# Overview

DocumentGenerator is a .NET 10 service that renders structured JSON templates into PDFs using a pooled Chromium instance via PuppeteerSharp.

It is designed to scale from a local console tool up to a distributed Kafka-backed service with minimal code changes.

## Modes of operation

### File mode (default, local dev)

Set `Kafka:Enabled: false` in `appsettings.json`. The app scans the `templates/` directory, renders every `*.json` template to PDF, and writes the output to `output/`.

```
templates/
  sample-badge.json           ← references badge.html + badge.css
  sample-badge-pulse-a6.json  ← Pulse design, A6 size
  sample-invoice.json
  ...

output/
  badge_<jobId>.pdf
  invoice_<jobId>.pdf
```

### Kafka mode (production / event)

Set `Kafka:Enabled: true`. The app runs as a long-lived consumer, polling the `render.requests` topic and publishing results to `render.results`.

## Included templates

### Badge designs

Seven ready-to-use badge designs — see [Badge Designs](badge-designs.md) for a full reference.

| JSON file | Design | Size |
|---|---|---|
| `sample-badge.json` | Original purple gradient | Credit-card (85.6×54 mm) |
| `sample-badge-pulse-a6.json` | Pulse — navy/purple diagonal stripe | A6 (105×148 mm) |
| `sample-badge-pulse-cc.json` | Pulse compact | Credit-card |
| `sample-badge-carbon-a6.json` | Carbon — black + neon-lime, terminal | A6 |
| `sample-badge-carbon-cc.json` | Carbon compact | Credit-card |
| `sample-badge-executive-a6.json` | Executive — charcoal + gold serif | A6 |
| `sample-badge-executive-cc.json` | Executive compact | Credit-card |

### Invoice

`sample-invoice.json` — a simple A4 invoice with line items, totals, and branding.

## Running the docs locally

```bash
dotnet tool restore
dotnet docfx docfx.json --serve
```

Then open [http://localhost:8080](http://localhost:8080).

---
_layout: landing
---

# DocumentGenerator

A high-throughput document and badge rendering service built on **.NET 10**, **PuppeteerSharp** (Chromium), **Handlebars.Net**, and **Confluent Kafka**.

> Designed for iPad → Kafka → PDF badge printing at live events, but equally usable as a general-purpose HTML-to-PDF microservice.

---

## Features

| | |
|---|---|
| **Handlebars templating** | Define documents as JSON with separate `.html`/`.css` files; inject variables at render time |
| **QR & barcode generation** | `{{qrCode}}` and `{{barCode}}` helpers emit inline SVG — no external service required |
| **Seven badge designs** | Three personalities (Pulse, Carbon, Executive) × two sizes (A6 + credit-card), all dark & bold |
| **Chromium browser pool** | Reuse Chromium instances across concurrent renders for maximum throughput |
| **Kafka integration** | Consume `render.requests`, publish to `render.results` and `render.deadletter` |
| **File mode** | Run locally without Kafka — scans `templates/`, writes PDFs to `output/` |
| **Pluggable architecture** | Swap queue providers, renderers, or template engines via interfaces in `DocumentGenerator.Core` |

---

## Quick Start

```bash
# 1. Start Kafka
docker compose -f docker-compose.kafka.yml up -d

# 2. Run the generator (Kafka mode)
dotnet run --project src/DocumentGenerator.Console

# 3. Send a test badge request
dotnet run --project tools/DocumentGenerator.TestProducer
```

Or without Kafka — set `Kafka:Enabled: false` in `appsettings.json` and run the generator directly.

---

## Browse the docs

- [Getting Started](articles/getting-started.md) — step-by-step from clone to first PDF
- [Architecture](articles/architecture.md) — solution layout and request flow
- [Template Schema](articles/template-schema.md) — full JSON reference including `htmlPath`/`cssPath`
- [Handlebars Helpers](articles/handlebars-helpers.md) — all built-in helpers with examples
- [Badge Designs](articles/badge-designs.md) — the seven included badge templates
- [Kafka Flow](articles/kafka-flow.md) — topics, message shapes, retry policy
- [Browser Pool](articles/browser-pool.md) — Chromium pool configuration and sizing
- [Testing](articles/testing.md) — running the test suite
- [API Reference](api/index.md) — auto-generated from XML documentation

---

## Project Structure

| Project | Purpose |
|---|---|
| `DocumentGenerator.Core` | Domain models and service interfaces (no third-party deps) |
| `DocumentGenerator.Templating` | Handlebars.Net engine, `{{qrCode}}`, `{{barCode}}`, file resolver |
| `DocumentGenerator.Pdf` | PuppeteerSharp renderer + Chromium browser pool |
| `DocumentGenerator.Messaging` | Rebus/Kafka consumer and producer |
| `DocumentGenerator.Console` | Host entry point, Spectre.Console TUI, file-mode worker |
| `tools/DocumentGenerator.TestProducer` | Interactive CLI for sending Kafka render requests |

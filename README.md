# DocumentGenerator — Badge Printing Platform

A .NET 10 platform for rendering and printing event badges on demand.
An iPad at an event check-in desk sends attendee data to a lightweight **Bridge** service running on the venue's local machine. The Bridge calls the cloud-hosted **API**, which either renders inline or offloads the work to the **Console** render worker via **Kafka** for high-throughput events. The finished PDF is returned to the iPad and simultaneously spooled to the locally attached badge printer.

---

## Architecture

### Kafka mode — recommended for production

```
[iPad]
  │  POST /print  (local network)
  ▼
[Bridge]                   ← venue Windows / Linux / macOS machine
  │  POST /api/badges/render  (HTTPS + X-Api-Key)
  ▼
[API]                      ← cloud-hosted ASP.NET Core Web API
  │  publish DocumentRenderRequest → render.requests
  ▼
[Kafka]
  │  consume render.requests
  ▼
[Console]                  ← one or more render workers (Chromium pool)
  │  Handlebars → HTML → Chromium → PDF/PNG bytes
  │  publish DocumentRenderResult → render.results
  ▼
[Kafka render.results]
  │  consumed by the API instance that published the request
  ▼
[API]  → awaiter resolved → Base64 document response
  │
  ▼
[Bridge] → iPad (preview) + local printer (OS print spooler)
```

Each API instance subscribes to `render.results` with a **unique consumer group**, so every result message reaches every instance — only the one that published the original request resolves its awaiter.

### Inline mode — simple / standalone

When `Kafka:Enabled = false` the API renders in-process using its embedded Chromium pool. No Kafka required — ideal for low-volume deployments or local development.

```
[iPad] → [Bridge] → [API (inline Chromium)] → [Bridge] → [iPad + Printer]
```

### Console direct mode — batch / load testing

The Console and TestProducer tool can be used without the API or Bridge:

```
[TestProducer] → Kafka render.requests → [Console] → Kafka render.results → [TestProducer]
```

---

## Solution structure

```
src/
  DocumentGenerator.Core          Domain models, interfaces, error types — no I/O
  DocumentGenerator.Templating    Handlebars engine, QR/barcode helpers, file resolver
  DocumentGenerator.Pdf           Chromium browser pool (PuppeteerSharp) + PDF/PNG renderer
  DocumentGenerator.Messaging     Rebus/Kafka message contracts + render request handler
  DocumentGenerator.Console       Render worker: Kafka consumer + Spectre.Console TUI
  DocumentGenerator.Api           Cloud REST API — Kafka or inline render path
  DocumentGenerator.Bridge        Venue proxy: HTTP relay + OS print spooler

tools/
  DocumentGenerator.TestProducer  Headless Kafka smoke-test / load-test client

tests/
  DocumentGenerator.UnitTests        245 tests — all dependencies mocked, no I/O
  DocumentGenerator.IntegrationTests  65 tests — WebApplicationFactory + Testcontainers Kafka

powershell/                        Dev and smoke-test scripts (see Scripts section)
templates/                         Shared Handlebars badge templates
docker-compose.full.yml            Full stack: Kafka + observability + all three services
docker-compose.kafka.yml           Kafka + Zookeeper + Kafka UI only
docker-compose.observability.yml   Grafana + Loki + Tempo + Prometheus + OTel Collector
Generated/                         Dev/test render output
```

---

## Prerequisites

- **.NET 10 SDK**
- **Docker Desktop** (for the full stack and integration tests)
- Chromium is downloaded automatically by PuppeteerSharp on first run (or `google-chrome-stable` is used inside Docker)

---

## Quick start — full stack in Docker

### 1. Configure environment

```bash
cp .env.example .env
```

The defaults in `.env.example` work out of the box — API key is `dev-api-key-insecure`. No further edits needed for local dev.

### 2. Start everything

```bash
docker compose -f docker-compose.full.yml up --build
```

First run takes a few minutes while Docker builds the three .NET images and Kafka starts up. Subsequent starts are fast (images cached).

### 3. Verify services

| Service | URL | Expected |
|---|---|---|
| API | http://localhost:8080/health | `{"status":"Healthy"}` |
| Bridge | http://localhost:5100/health | `{"status":"Healthy"}` |
| Kafka UI | http://localhost:8090 | Browse topics and consumer groups |
| Grafana | http://localhost:3000 | Traces, logs, metrics — login: admin / admin |
| Prometheus | http://localhost:9090 | Raw metrics |

> The API and Bridge containers may show "unhealthy" in `docker ps` — this is a known false alarm because the Docker health check script uses `curl`, which is not present in the distroless runtime image. Both services respond correctly on their ports.

### 4. Render a badge

```bash
curl -s -X POST http://localhost:5100/render \
  -H "Content-Type: application/json" \
  -H "X-Api-Key: dev-api-key-insecure" \
  -d '{
    "templateName": "badge-pulse-a6",
    "format": "Png",
    "variables": {
      "firstName": "Ada",
      "lastName":  "Lovelace",
      "jobTitle":  "Mathematician",
      "company":   "Analytical Engine Co"
    }
  }' | jq -r .documentBase64 | base64 -d > badge.png
```

### 5. Stop / reset

```bash
# Stop, keep volumes
docker compose -f docker-compose.full.yml down

# Stop and wipe all volumes (full reset)
docker compose -f docker-compose.full.yml down -v
```

### Scale render workers

```bash
docker compose -f docker-compose.full.yml up --scale console=3
```

### Startup dependency order

```
Zookeeper → Kafka (healthy) → kafka-init (topics created)
                                          ↓
                             otel-collector (healthy)
                                     ↓             ↓
                                   Api           Console
                                     ↓
                                  Bridge
```

---

## Local development

### Path A — Kafka mode (three terminals)

**Terminal 1 — Start Kafka**
```bash
docker compose -f docker-compose.kafka.yml up -d
```

**Terminal 2 — Console render worker**
```bash
dotnet run --project src/DocumentGenerator.Console
```
Wait for: `Rebus subscription active — listening for DocumentRenderRequest`

**Terminal 3 — API**
```bash
dotnet run --project src/DocumentGenerator.Api --launch-profile http
```
`appsettings.Development.json` sets `Kafka:Enabled = true`. Listens on **http://localhost:7071**.

**Terminal 4 — Bridge**
```bash
dotnet run --project src/DocumentGenerator.Bridge --launch-profile http
```
Listens on **http://localhost:5100**.

**Trigger a render:**
```bash
curl -X POST http://localhost:5100/print \
  -H "Content-Type: application/json" \
  -d '{
    "templateName": "badge-pulse-a6",
    "variables": {
      "firstName": "Ada", "lastName": "Lovelace",
      "jobTitle": "Mathematician", "company": "Analytical Engine Co"
    }
  }'
```

### Path B — Inline mode (no Kafka, two terminals)

**Terminal 1 — API**
```bash
ASPNETCORE_ENVIRONMENT=Production \
  ApiAuth__ApiKey=my-local-key \
  Kafka__Enabled=false \
  dotnet run --project src/DocumentGenerator.Api
```

**Terminal 2 — Bridge**
```bash
dotnet run --project src/DocumentGenerator.Bridge --launch-profile http
```

Then POST to `http://localhost:5100/print` as above.

### Path C — Console + TestProducer (Kafka direct)

No API or Bridge required. Useful for batch rendering and load testing.

**Terminal 1 — Console**
```bash
dotnet run --project src/DocumentGenerator.Console
```

**Terminal 2 — TestProducer**
```bash
dotnet run --project tools/DocumentGenerator.TestProducer
```

---

## Configuration reference

### DocumentGenerator.Api

| Key | Default | Description |
|---|---|---|
| `ApiAuth:ApiKey` | `CHANGE-ME-IN-PRODUCTION` | Required `X-Api-Key` header value |
| `Cors:AllowedOrigins` | `*` | Comma-separated origins. Use explicit value in production |
| `Kafka:Enabled` | `false` | `true` = Kafka path; `false` = inline Chromium |
| `Kafka:BootstrapServers` | `localhost:9092` | Kafka broker address(es) |
| `Kafka:RequestTopic` | `render.requests` | Topic for outbound render jobs |
| `Kafka:ResultTopic` | `render.results` | Topic for inbound render results |
| `Kafka:ResultTimeoutSeconds` | `25` | Max wait before returning HTTP 504 |
| `BrowserPool:MaxSize` | `4` | Max concurrent Chromium instances (inline mode only) |

### DocumentGenerator.Console

| Key | Default | Description |
|---|---|---|
| `Kafka:Enabled` | `true` | Must be `true` to act as a render worker |
| `Kafka:BootstrapServers` | `localhost:9092` | Kafka broker address(es) |
| `Kafka:ConsumerGroupId` | `document-generator` | Shared group — all Console instances share load |
| `Kafka:MaxConcurrentRenders` | `4` | Rebus worker thread count |
| `BrowserPool:MaxSize` | `4` | Max concurrent Chromium instances |

### DocumentGenerator.Bridge

| Key | Default | Description |
|---|---|---|
| `Bridge:Port` | `5100` | Local HTTP listen port |
| `Bridge:IsConfigured` | `false` | Set `true` once Cloud URL and key are saved |
| `Cloud:BaseUrl` | _(empty)_ | URL of the cloud-hosted API |
| `Cloud:ApiKey` | _(empty)_ | API key matching `ApiAuth:ApiKey` on the API |
| `Cloud:Timeout` | `00:00:30` | HTTP timeout per render request |
| `Printer:DefaultPrinterName` | `null` | Fallback printer when not specified per-request |

---

## Security

### API key

The `X-Api-Key` header is validated on every request except `/health`.

1. Generate a key: `openssl rand -hex 32`
2. Set on the API: `ApiAuth__ApiKey=<key>`
3. Set on the Bridge: `Cloud__ApiKey=<key>`
4. Never commit a real key. The placeholder `CHANGE-ME-IN-PRODUCTION` causes an intentional startup warning.

### CORS

In production, replace `*` with the explicit Bridge address:
```
Cors__AllowedOrigins=http://192.168.1.100:5100
```

### Kafka SASL/TLS (Confluent Cloud / MSK)

```
Kafka__SecurityProtocol=SaslSsl
Kafka__SaslMechanism=ScramSha256
Kafka__SaslUsername=<api-key>
Kafka__SaslPassword=<api-secret>
```

### Secret management

Copy `.env.example` to `.env` — it is already in `.gitignore`.

For production use your platform's secret store:
- **Azure**: Application Settings or Key Vault references
- **AWS**: Parameter Store / Secrets Manager with ECS task role
- **Kubernetes**: `Secret` objects as environment variables

---

## Tests

```bash
# All 310 tests
dotnet test DocumentGenerator.sln

# Unit tests only (fast, no Docker)
dotnet test tests/DocumentGenerator.UnitTests

# Integration tests (WebApplicationFactory + Testcontainers Kafka)
dotnet test tests/DocumentGenerator.IntegrationTests
```

| Suite | Count | Notes |
|---|---|---|
| Unit | 245 | Controllers, pipeline, templating, Kafka store/handler, messaging — all mocked |
| Integration | 65 | API endpoints, Bridge endpoints, pipeline with real Handlebars, Kafka round-trip via Testcontainers |

---

## PowerShell scripts

All scripts live in `powershell/`. Run from the repo root:

```powershell
powershell -ExecutionPolicy Bypass -File powershell\<script>.ps1
```

| Script | Purpose |
|---|---|
| `check-health.ps1` | Probes API (`:7071`) and Bridge (`:5100`) health endpoints |
| `check-templates.ps1` | Lists available templates via Bridge |
| `render-both.ps1` | Renders all 4 templates × 2 formats (PDF + PNG) via Bridge, saves to `Generated/`, opens all files |
| `debug-api.ps1` | Hits the API directly on `:7071` — lists templates, renders PDF and PNG |
| `debug-bridge.ps1` | End-to-end connectivity check: direct API call then via Bridge |
| `fire-bridge.ps1` | Waits for Bridge to be ready, then renders PDF + lists templates and printers |
| `fire-bridge-png.ps1` | Waits for Bridge to be ready, then renders PNG |
| `fire-png-test.ps1` | One-shot PNG render direct to Bridge, prints image dimensions |
| `wait-and-fire.ps1` | Waits for API to be ready, renders a PDF, opens it |
| `test-render.ps1` | Minimal one-shot render test direct to API |

### Render all badge variants

```powershell
# Requires the full stack running (docker compose -f docker-compose.full.yml up -d)
powershell -ExecutionPolicy Bypass -File powershell\render-both.ps1
```

Output written to `Generated/`:

| File | Dimensions / Size | Template |
|---|---|---|
| `ada-badge-pulse-a6.png` | 794×1120 px | A6 portrait — Pulse theme |
| `ada-badge-pulse-a6.pdf` | ~19 KB | A6 portrait — Pulse theme |
| `ada-badge-pulse-cc.png` | 648×410 px | Credit card — Pulse theme |
| `ada-badge-pulse-cc.pdf` | ~18 KB | Credit card — Pulse theme |
| `ada-badge-executive-cc.png` | 648×410 px | Credit card — Executive theme |
| `ada-badge-executive-cc.pdf` | ~35 KB | Credit card — Executive theme |
| `ada-badge-carbon-cc.png` | 648×410 px | Credit card — Carbon theme |
| `ada-badge-carbon-cc.pdf` | ~22 KB | Credit card — Carbon theme |

PNGs use `DeviceScaleFactor=2` (retina quality) and are clipped exactly to the badge boundary via `getBoundingClientRect()`.

---

## Badge templates

| Template name | Size | Theme |
|---|---|---|
| `badge-pulse-a6` | A6 (105×148mm) | Vibrant purple gradient, diagonal stripe, QR code |
| `badge-pulse-cc` | Credit card (85.6×54mm) | Pulse theme, compact |
| `badge-executive-a6` | A6 | Dark navy, branding-driven gold accent |
| `badge-executive-cc` | Credit card | Executive theme, compact |
| `badge-carbon-a6` | A6 | Dark monochrome carbon aesthetic |
| `badge-carbon-cc` | Credit card | Carbon theme, compact |

List available templates at runtime:
```
GET /api/badges/templates   (X-Api-Key required)
GET /templates              (via Bridge)
```

---

## Render request format

`POST /api/badges/render` (direct to API), `POST /render` or `POST /print` (via Bridge):

```json
{
  "templateName": "badge-pulse-a6",
  "correlationId": "optional-guid",
  "format": "Pdf",
  "variables": {
    "firstName":   "Ada",
    "lastName":    "Lovelace",
    "jobTitle":    "Mathematician",
    "company":     "Analytical Engine Co",
    "ticketType":  "Speaker",
    "attendeeId":  "TC2026-001",
    "sessionName": "Hall A — Keynote",
    "eventDate":   "12–14 March 2026",
    "eventVenue":  "ExCeL London"
  },
  "branding": {
    "companyName":     "TechConf 2026",
    "primaryColour":   "#6C3CE1",
    "secondaryColour": "#F3F0FF",
    "bodyFont":        "Segoe UI, Arial, sans-serif"
  }
}
```

`format` accepts `Pdf` or `Png`. The response always includes `documentBase64`, `mimeType`, `success`, `correlationId`, `elapsedTime`, and `completedAt`.

Sample payloads for all templates: `src/DocumentGenerator.Console/templates/sample-*.json`

---

## Observability

All three services emit OpenTelemetry traces, metrics, and logs to the OTel Collector when `OpenTelemetry:Enabled = true`.

```bash
docker compose -f docker-compose.observability.yml up -d
```

| Service | URL |
|---|---|
| Grafana | http://localhost:3000 (admin / admin) |
| Prometheus | http://localhost:9090 |
| Loki (logs) | via Grafana |
| Tempo (traces) | via Grafana |

Custom metrics: `documentgenerator.render.duration_ms` (histogram), `documentgenerator.render.count` (counter) — tagged by `document_type` and `success`.

---

## CI

GitHub Actions workflow (`.github/workflows/dotnet.yml`) runs on every push to `main`:

1. `dotnet restore`
2. `dotnet build -warnaserror` — zero warnings enforced
3. `dotnet test` — all 310 tests must pass (integration tests use Testcontainers, Docker is available on the runner)

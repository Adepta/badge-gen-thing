# DocumentGenerator — Badge Printing Platform

A .NET 10 platform for rendering and printing event badges on demand.
An iPad at an event check-in desk sends attendee data to a lightweight
**Bridge** service running on the venue's local machine. The Bridge
calls the cloud-hosted **API**, which either renders inline or offloads
the work to the **Console** render service via **Kafka** for high-throughput
events. The finished PDF is returned to the iPad and simultaneously spooled
to the locally attached badge printer.

---

## Architecture

### Recommended — Kafka mode (high throughput)

```
[iPad]
  │  POST /print  (local network, no auth)
  ▼
[Bridge]                   ← runs on venue Windows/Linux/macOS machine
  │  POST /api/badges/render  (HTTPS + X-Api-Key)
  ▼
[API]                      ← cloud-hosted ASP.NET Core Web API
  │  publish DocumentRenderRequest → Kafka render.requests
  ▼
[Kafka]                    ← managed broker (Confluent Cloud / MSK / self-hosted)
  │  consume render.requests
  ▼
[Console]                  ← one or more render workers (Chromium pool)
  │  Handlebars → HTML → Chromium → PDF bytes
  │  publish DocumentRenderResult → Kafka render.results
  ▼
[Kafka render.results]
  │  consumed by the API instance that published the request
  ▼
[API]  → awaiter resolved → Base64 PDF response
  │
  ▼
[Bridge] → iPad (preview) + local printer (OS print spooler)
```

Why Kafka in the middle?
- The API returns to the Bridge immediately after publishing; no HTTP thread is held open during rendering.
- Multiple Console instances consume from `render.requests` in a shared group → automatic horizontal scaling.
- Each API instance subscribes with a **unique consumer group** so every result message is delivered to every instance; only the one that published the request resolves the awaiter.

### Fallback — inline mode (simple / standalone)

When `Kafka:Enabled = false` in the API config, rendering runs in-process
using the embedded Chromium pool. No Kafka dependency — useful for low-volume
deployments or local development.

```
[iPad] → [Bridge] → [API (inline Chromium render)] → [Bridge] → [iPad + Printer]
```

### Kafka only — Console direct mode

The Console can also be used standalone, without the API/Bridge. The TestProducer
tool publishes directly to `render.requests` and receives results on `render.results`.
Use this for batch rendering, load testing, or CI smoke tests.

```
[TestProducer] → Kafka render.requests → [Console] → Kafka render.results → [TestProducer]
```

---

## Solution structure

```
src/
  DocumentGenerator.Core          Pure domain — interfaces, models, no I/O
  DocumentGenerator.Templating    Handlebars engine + file-based template resolver
  DocumentGenerator.Pdf           Chromium browser pool (PuppeteerSharp) + PDF renderer
  DocumentGenerator.Messaging     Rebus/Kafka message handler + contracts
  DocumentGenerator.Console       Render worker: Kafka consumer + Spectre TUI dashboard
  DocumentGenerator.Api           Cloud-hosted REST API (Kafka or inline render)
  DocumentGenerator.Bridge        Venue-side proxy: HTTP relay + OS print spooler

tools/
  DocumentGenerator.TestProducer  Headless Kafka test client (round-trip smoke test)

tests/
  DocumentGenerator.UnitTests        xUnit — no I/O, all dependencies mocked
  DocumentGenerator.IntegrationTests xUnit — WebApplicationFactory + real pipeline

templates/                         Shared badge & invoice Handlebars templates
docker-compose.kafka.yml           Local Kafka + Zookeeper + Kafka UI
docker-compose.observability.yml   Grafana + Loki + Tempo + Prometheus + OTel Collector
docker/                            OTel Collector and observability stack config
Generated/                         Dev/test PDF output (LocalFileAdapter writes here)
```

---

## Prerequisites

- **.NET 10 SDK**
- **Docker Desktop** (for local Kafka and observability stack)
- Chromium is downloaded automatically by PuppeteerSharp on first run

---

## Quick start — full stack in Docker

Run everything — Kafka, OTel, Grafana, and all three services — with a single command.

### 1. Copy and configure `.env`

```bash
cp .env.example .env
```

Open `.env` and set `API_KEY` to any random string (it is required — the stack won't start without it):

```bash
# Linux / macOS
API_KEY=$(openssl rand -hex 32)
sed -i "s/CHANGE-ME-IN-PRODUCTION/$API_KEY/" .env

# Windows PowerShell
$key = -join ((48..57+65..90+97..122) | Get-Random -Count 32 | % {[char]$_})
(Get-Content .env) -replace 'CHANGE-ME-IN-PRODUCTION', $key | Set-Content .env
```

### 2. Start the full stack

```bash
docker compose -f docker-compose.full.yml up --build
```

First run takes a few minutes — Docker builds three .NET images and Kafka waits for Zookeeper.
Subsequent starts are fast (images are cached).

### 3. Verify everything is up

| Service | URL | Notes |
|---|---|---|
| **Api** | http://localhost:8080/health | Should return `{"status":"Healthy"}` |
| **Bridge** | http://localhost:5100/health | Should return `{"status":"Healthy"}` |
| **Kafka UI** | http://localhost:8090 | Browse topics, consumer groups, messages |
| **Grafana** | http://localhost:3000 | Traces, logs, metrics — login: admin / admin |
| **Prometheus** | http://localhost:9090 | Raw metrics |

### 4. Render a badge

```bash
curl -s -X POST http://localhost:5100/render \
  -H "Content-Type: application/json" \
  -H "X-Api-Key: <your API_KEY from .env>" \
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

### 5. Stop and clean up

```bash
# Stop all containers but keep volumes (Kafka state, Grafana dashboards)
docker compose -f docker-compose.full.yml down

# Stop and wipe all volumes (full reset)
docker compose -f docker-compose.full.yml down -v
```

### Startup order

Docker Compose enforces this dependency chain automatically:

```
Zookeeper → Kafka (healthy) → kafka-init (topics created)
                                          ↓
                             otel-collector (healthy)
                                     ↓             ↓
                                   Api           Console
                                     ↓
                                  Bridge
```

### Scale the render worker

```bash
# Run 3 Console render workers in parallel
docker compose -f docker-compose.full.yml up --scale console=3
```

---

## Quick start — local development (both paths)

### 1. Start Kafka

```bash
docker compose -f docker-compose.kafka.yml up -d
```

Kafka UI is available at **http://localhost:8080**.

### 2. Configure secrets

```bash
cp .env.example .env
# Edit .env — at minimum set ApiAuth__ApiKey to a random string
```

For local dev the defaults in `appsettings.Development.json` work out of the box
(`dev-api-key-insecure`). Do not use these in production.

---

## Running — Path A: Kafka mode

Open **three terminals**.

**Terminal 1 — Console render worker**
```bash
cd src/DocumentGenerator.Console
dotnet run
```
Waits for Chromium to download, then connects to Kafka and shows the Spectre TUI.
Watch for: `Rebus subscription active — listening for DocumentRenderRequest`

**Terminal 2 — API (Kafka enabled in Development)**
```bash
cd src/DocumentGenerator.Api
dotnet run --launch-profile http
```
`appsettings.Development.json` sets `Kafka:Enabled = true`.
Listens on **http://localhost:7071**.
Watch for: `Kafka consumer group api-<guid> subscribed`

**Terminal 3 — Bridge**
```bash
cd src/DocumentGenerator.Bridge
dotnet run --launch-profile http
```
Listens on **http://localhost:5100**.
Watch for: `Application started`

**Trigger a print** (Terminal 4 or Postman):
```bash
curl -X POST http://localhost:5100/print \
  -H "Content-Type: application/json" \
  -d '{
    "templateName": "badge-pulse-a6",
    "variables": {
      "firstName": "Jane",
      "lastName":  "Smith",
      "jobTitle":  "Engineer",
      "company":   "Acme Corp",
      "ticketType":"Speaker",
      "attendeeId":"TC2026-001"
    }
  }'
```

**What to watch:**
| Component | Log line |
|---|---|
| Bridge | `Cloud render request — CorrelationId=...` |
| API | `Render request published to Kafka — CorrelationId=...` |
| Console | `Handling render request` → `Chromium render complete` |
| API | `Render result resolved — CorrelationId=... Success=True` |
| Bridge | `Cloud render complete — Success=True` |
| `Generated/` | PDF file written by LocalFileAdapter (Development mode) |

Kafka message flow is visible in **Kafka UI → http://localhost:8080**.

---

## Running — Path B: Inline mode (no Kafka)

Disable Kafka in the API by setting `Kafka__Enabled=false` (or use a non-Development environment).

Open **two terminals**.

**Terminal 1 — API**
```bash
cd src/DocumentGenerator.Api
ASPNETCORE_ENVIRONMENT=Production \
  ApiAuth__ApiKey=my-local-key \
  Kafka__Enabled=false \
  dotnet run
```

**Terminal 2 — Bridge**
```bash
cd src/DocumentGenerator.Bridge
ASPNETCORE_ENVIRONMENT=Development \
  dotnet run --launch-profile http
```

Then POST to `http://localhost:5100/print` as above.

---

## Running — Path C: Console + TestProducer (Kafka direct)

No API or Bridge involved. Useful for batch rendering and load testing.

**Terminal 1 — Console**
```bash
cd src/DocumentGenerator.Console
dotnet run
```

**Terminal 2 — TestProducer**
```bash
cd tools/DocumentGenerator.TestProducer
dotnet run
```

The producer sends one render job every 30 seconds (configurable via `Producer:ScheduleInterval`),
waits for the result, and saves the PDF to the `Generated/` folder or logs the path.

---

## Configuration reference

### DocumentGenerator.Api

| Key | Default | Description |
|---|---|---|
| `ApiAuth:ApiKey` | `CHANGE-ME-IN-PRODUCTION` | API key required in `X-Api-Key` header |
| `Cors:AllowedOrigins` | `*` | Comma-separated origins. Use `*` for dev only |
| `Kafka:Enabled` | `false` | `true` = Kafka path; `false` = inline Chromium |
| `Kafka:BootstrapServers` | `localhost:9092` | Kafka broker(s) |
| `Kafka:RequestTopic` | `render.requests` | Topic to publish render jobs to |
| `Kafka:ResultTopic` | `render.results` | Topic to consume results from |
| `Kafka:ResultTimeoutSeconds` | `25` | Max wait for render result before 504 |
| `BrowserPool:MaxSize` | `4` | Max concurrent Chromium instances (inline mode) |

### DocumentGenerator.Console

| Key | Default | Description |
|---|---|---|
| `Kafka:Enabled` | `true` | Must be `true` to act as a render worker |
| `Kafka:BootstrapServers` | `localhost:9092` | Kafka broker(s) |
| `Kafka:ConsumerGroupId` | `document-generator` | Shared group — all Console instances share load |
| `Kafka:MaxConcurrentRenders` | `4` | Concurrency cap (matches Chromium pool size) |
| `BrowserPool:MaxSize` | `4` | Max concurrent Chromium instances |

### DocumentGenerator.Bridge

| Key | Default | Description |
|---|---|---|
| `Bridge:Port` | `5100` | Local HTTP listen port |
| `Bridge:IsConfigured` | `false` | Set to `true` once Cloud URL and key are configured |
| `Cloud:BaseUrl` | _(empty)_ | URL of the cloud-hosted API |
| `Cloud:ApiKey` | _(empty)_ | API key matching `ApiAuth:ApiKey` on the API |
| `Cloud:Timeout` | `00:00:30` | HTTP timeout per render request |
| `Printer:DefaultPrinterName` | `null` | OS default printer when not specified per-request |

---

## Security

### API key

The `X-Api-Key` header is validated on every API request except `/health`.
In production:

1. Generate a random key: `openssl rand -hex 32`
2. Set it as an environment variable: `ApiAuth__ApiKey=<generated-key>`
3. Set the same value in the Bridge: `Cloud__ApiKey=<generated-key>`
4. **Never commit a real key to source control.** The placeholder `CHANGE-ME-IN-PRODUCTION` will cause an intentional startup warning — you will see it if you forget.

### CORS

In production, replace `Cors:AllowedOrigins=*` with the explicit IP or hostname of your Bridge:
```
Cors__AllowedOrigins=http://192.168.1.100:5100
```

### Kafka SASL/TLS (Confluent Cloud / MSK)

Set these environment variables on all services that connect to Kafka:
```
Kafka__SecurityProtocol=SaslSsl
Kafka__SaslMechanism=ScramSha256
Kafka__SaslUsername=<api-key>
Kafka__SaslPassword=<api-secret>
```

### Secret management

- Copy `.env.example` to `.env` — it is already in `.gitignore`.
- For production deployments use your platform's secret store:
  - **Azure App Service / Container Apps**: Application Settings / Key Vault references
  - **AWS**: Parameter Store / Secrets Manager with ECS task role
  - **Kubernetes**: `Secret` objects referenced as environment variables

---

## Tests

```bash
# All tests (310 total)
dotnet test DocumentGenerator.sln

# Unit tests only (fast, no Docker)
dotnet test tests/DocumentGenerator.UnitTests

# Integration tests (uses WebApplicationFactory, no real Kafka needed)
dotnet test tests/DocumentGenerator.IntegrationTests
```

**Test coverage:**
- 245 unit tests — controllers, pipeline, templating engine, Kafka store/handler, messaging
- 65 integration tests — API endpoints, Bridge endpoints, pipeline + real Handlebars

---

## Local smoke test (PowerShell)

These scripts let you render real badges against the running stack and open the output files.
All output is written to `Generated/` in the repo root.

### Prerequisites

All three services must be running (see [Quick start](#quick-start--local-development-both-paths)).
Kafka must be up: `docker compose -f docker-compose.kafka.yml up -d`

### Scripts

| Script | What it does |
|---|---|
| `check-health.ps1` | Confirms Api (:7071) and Bridge (:5100) are responding |
| `render-both.ps1` | Renders every badge template as both PDF and PNG, opens them all |

### Run the smoke test

```powershell
# 1. Confirm services are healthy
powershell -ExecutionPolicy Bypass -File check-health.ps1

# 2. Render all templates (A6 + 3x credit card) as PDF and PNG, opens them automatically
powershell -ExecutionPolicy Bypass -File render-both.ps1
```

Output files written to `Generated/`:

| File | Size | Format |
|---|---|---|
| `ada-badge-pulse-a6.png` | 794×1120 px (2× retina) | A6 portrait badge |
| `ada-badge-pulse-a6.pdf` | ~61 KB | A6 portrait badge |
| `ada-badge-pulse-cc.png` | 648×410 px (2× retina) | Credit card badge |
| `ada-badge-pulse-cc.pdf` | ~37 KB | Credit card badge |
| `ada-badge-executive-cc.png` | 648×410 px (2× retina) | Credit card badge |
| `ada-badge-executive-cc.pdf` | ~34 KB | Credit card badge |
| `ada-badge-carbon-cc.png` | 648×410 px (2× retina) | Credit card badge |
| `ada-badge-carbon-cc.pdf` | ~21 KB | Credit card badge |

PNG dimensions are badge-exact — the Chromium viewport is sized to the rendered document
using `getBoundingClientRect()` before screenshotting, so you get the badge and nothing else.
`DeviceScaleFactor=2` gives retina-quality output without changing CSS layout.

### Render a single badge via curl / Postman

Direct to Bridge `/render` endpoint (returns JSON with `documentBase64`):

```bash
curl -s -X POST http://localhost:5100/render \
  -H "Content-Type: application/json" \
  -H "X-Api-Key: dev-api-key-insecure" \
  -d '{
    "templateName": "badge-pulse-cc",
    "format": "Png",
    "variables": {
      "firstName":  "Ada",
      "lastName":   "Lovelace",
      "jobTitle":   "Mathematician",
      "company":    "Analytical Engine Co"
    }
  }' | jq -r .documentBase64 | base64 -d > badge.png
```

---

## Observability (optional)

```bash
docker compose -f docker-compose.observability.yml up -d
```

| Service | URL |
|---|---|
| Grafana | http://localhost:3000 (admin / admin) |
| Prometheus | http://localhost:9090 |
| Loki (logs) | via Grafana |
| Tempo (traces) | via Grafana |

All services emit OpenTelemetry traces, metrics, and logs to the OTel Collector
on `http://localhost:4317` when `OpenTelemetry:Enabled = true`.

---

## Available badge templates

| Template name | Size | Description |
|---|---|---|
| `badge-pulse-a6` | A6 (105×148mm) | Modern gradient design |
| `badge-pulse-cc` | Credit card (85.6×54mm) | Compact version |
| `badge-executive-a6` | A6 | Clean minimal design |
| `badge-executive-cc` | Credit card | Compact version |
| `badge-carbon-a6` | A6 | Dark carbon theme |
| `badge-carbon-cc` | Credit card | Compact version |

List available templates at runtime:
```
GET /api/badges/templates  (X-Api-Key required)
```

---

## Render request format

`POST /api/badges/render` (direct to API) or `POST /print` / `POST /render` (via Bridge):

```json
{
  "templateName": "badge-pulse-a6",
  "correlationId": "optional-guid",
  "format": "Pdf",
  "variables": {
    "firstName":   "Jane",
    "lastName":    "Smith",
    "jobTitle":    "Senior Engineer",
    "company":     "Acme Corp",
    "ticketType":  "Speaker",
    "attendeeId":  "TC2026-00842",
    "sessionName": "Hall A — Keynote",
    "eventDate":   "12–14 March 2026",
    "eventVenue":  "ExCeL London"
  },
  "branding": {
    "companyName":      "TechConf 2026",
    "primaryColour":    "#6C3CE1",
    "secondaryColour":  "#F3F0FF",
    "bodyFont":         "Segoe UI, Arial, sans-serif"
  }
}
```

Sample payloads for all templates are in `src/DocumentGenerator.Console/templates/sample-*.json`.

# Getting Started

This guide walks you from a fresh clone to your first rendered PDF in under five minutes.

## Prerequisites

| Tool | Version | Notes |
|---|---|---|
| [.NET SDK](https://dotnet.microsoft.com/download) | 10.0+ | `dotnet --version` to check |
| [Docker Desktop](https://www.docker.com/products/docker-desktop/) | any recent | Only needed for Kafka mode |

---

## 1. Clone and restore

```bash
git clone https://github.com/your-org/DocumentGenerator.git
cd DocumentGenerator
dotnet restore
```

---

## 2. Run in file mode (no Kafka required)

File mode is the fastest way to verify everything works.

**a)** Open `src/DocumentGenerator.Console/appsettings.json` and ensure:

```json
"Kafka": {
  "Enabled": false
}
```

**b)** Run the service:

```bash
dotnet run --project src/DocumentGenerator.Console
```

The Spectre.Console TUI starts. On first run, PuppeteerSharp downloads a pinned Chromium revision (~170 MB) — subsequent starts are instant.

**c)** The worker scans `templates/`, renders every `*.json` file, and writes PDFs to `output/`:

```
output/
  badge_<guid>.pdf
  badge-pulse-a6_<guid>.pdf
  badge-carbon-a6_<guid>.pdf
  badge-executive-a6_<guid>.pdf
  ... (one per template)
```

Press **Ctrl+C** to stop.

---

## 3. Run in Kafka mode

**a)** Start the Kafka stack:

```bash
docker compose -f docker-compose.kafka.yml up -d
```

Wait ~30 seconds for the broker health-check to pass:

```bash
docker compose -f docker-compose.kafka.yml ps
```

**b)** Start the service with Kafka enabled:

```bash
# appsettings.json: Kafka.Enabled = true
dotnet run --project src/DocumentGenerator.Console
```

**c)** In a second terminal, send a render request:

```bash
dotnet run --project tools/DocumentGenerator.TestProducer
```

A menu appears listing all seven badge variants plus the invoice. Select one; the TestProducer:
1. Loads the template JSON from `templates/`
2. Sends a `DocumentRenderRequest` to the `render.requests` Kafka topic
3. Waits for a `DocumentRenderResult` reply on `render.results`
4. Saves the decoded PDF to `tools/DocumentGenerator.TestProducer/Generated/`

---

## 4. Build and test

```bash
# Build the entire solution
dotnet build DocumentGenerator.sln

# Run unit tests (89 tests)
dotnet test tests/DocumentGenerator.UnitTests/DocumentGenerator.UnitTests.csproj

# Run integration tests (18 tests — no Kafka required)
dotnet test tests/DocumentGenerator.IntegrationTests/DocumentGenerator.IntegrationTests.csproj
```

See [Testing](testing.md) for a full breakdown of the test suite.

---

## 5. Browse the API documentation

```bash
dotnet tool restore
dotnet docfx docfx.json --serve
```

Open [http://localhost:8080](http://localhost:8080).

---

## Next steps

- [Template Schema](template-schema.md) — learn how JSON template files are structured
- [Handlebars Helpers](handlebars-helpers.md) — QR codes, barcodes, date formatting, and more
- [Badge Designs](badge-designs.md) — explore and customise the seven included badge templates
- [Architecture](architecture.md) — understand the request flow and extension points

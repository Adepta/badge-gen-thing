# Architecture

## Solution structure

```
DocumentGenerator/
├── src/
│   ├── DocumentGenerator.Core          # Interfaces, models, configuration
│   ├── DocumentGenerator.Templating    # Handlebars engine + file resolver
│   ├── DocumentGenerator.Pdf           # Chromium pool + PDF renderer
│   ├── DocumentGenerator.Messaging     # Rebus/Kafka consumer + producer
│   ├── DocumentGenerator.Console       # Host entry point, TUI, file-mode worker
│   ├── DocumentGenerator.Api           # Cloud ASP.NET Core Web API (badge rendering)
│   └── DocumentGenerator.Bridge        # Client-side bridge: iPad ↔ cloud ↔ local printer
├── tools/
│   └── DocumentGenerator.TestProducer  # Interactive Kafka test client
├── templates/                          # HTML, CSS, and JSON template files
├── tests/
│   ├── DocumentGenerator.UnitTests     # xUnit unit tests (170 tests)
│   └── DocumentGenerator.IntegrationTests  # Integration tests (61 tests)
└── docs/                               # This documentation (DocFX)
```

## Badge printing deployment

```
iPad (local Wi-Fi)
    │  POST /print  { templateName, variables }
    ▼
DocumentGenerator.Bridge  (local PC/server, port 5100)
    │  POST /api/badges/render  +  X-Api-Key
    ▼
DocumentGenerator.Api  (cloud / server)
    │  Chromium → PDF bytes
    ▼
DocumentGenerator.Bridge
    ├─► OS print spooler  (Windows: System.Drawing.Printing | Linux: CUPS lp)
    └─► 200 OK  { documentBase64, printed: true }
    ▼
iPad  (preview or confirmation)
```

## Dependency graph

```
Api
  ├── Core          (models + interfaces)
  ├── Templating    → Core
  └── Pdf           → Core

Bridge (standalone, no Core dep)

Console
  ├── Core
  ├── Templating    → Core
  ├── Pdf           → Core
  └── Messaging     → Core  (Rebus, Rebus.Kafka)
```

`Core` has zero third-party dependencies — all external libraries are contained within the implementation projects.

## Key interfaces (Core)

| Interface | Implementation | Purpose |
|---|---|---|
| `ITemplateEngine` | `HandlebarsTemplateEngine` | Renders Handlebars HTML+CSS → HTML string |
| `ITemplateContentResolver` | `FileTemplateContentResolver` | Loads `htmlPath`/`cssPath` files from disk |
| `IDocumentRenderer` | `PuppeteerDocumentRenderer` | Renders HTML → PDF bytes via Chromium |
| `IBrowserPool<T>` | `ChromiumBrowserPool` | Manages pooled Chromium instances |
| `IDocumentPipeline` | `DocumentPipeline` | Orchestrates template → PDF |
| `IRenderMetrics` | `RenderStats` | Thread-safe success/failure counters for the TUI |

### Bridge-specific interfaces

| Interface | Implementations | Purpose |
|---|---|---|
| `IPrinterAdapter` | `WindowsPrinterAdapter`, `CupsPrinterAdapter` | Abstracts OS printer API; selected at runtime by `PrinterAdapterFactory` |

## Render pipeline (Api / Console)

```
JSON template file  ─or─  POST /api/badges/render body
    │
    ▼ DocumentGeneratorWorker (file mode) / DocumentRenderRequestHandler (Kafka) / BadgesController (API)
    │  Deserialise / resolve → DocumentTemplate
    │
    ▼ ITemplateContentResolver.ResolveAsync()
    │  Load htmlPath + cssPath from disk → inline into DocumentTemplate
    │
    ▼ IDocumentPipeline.ExecuteAsync()
    │
    ├─► ITemplateEngine.RenderAsync()
    │       Handlebars: merge branding + variables into HTML
    │       CSS injected into <style> before </head>
    │       {{qrCode}} / {{barCode}} helpers emit inline SVG
    │
    └─► IDocumentRenderer.RenderPdfAsync()
            Lease browser from IBrowserPool
            Page.SetContentAsync(html)
            Page.PdfDataAsync() → byte[]
            Return lease to pool
    │
    ▼
RenderResult { JobId, PdfBytes, ElapsedTime }
```

## DI registration

Each project exposes an extension method on `IServiceCollection`:

```csharp
// Console + Api
services
    .AddTemplating()          // HandlebarsTemplateEngine + FileTemplateContentResolver
    .AddPdfRendering()        // ChromiumBrowserPool + PuppeteerDocumentRenderer + DocumentPipeline
    .AddRebusKafkaMessaging(kafkaOptions);  // Console only — Rebus, Kafka transport, handler

// Api only
services.AddAuthentication("ApiKey")
        .AddScheme<ApiKeyAuthenticationSchemeOptions, ApiKeyAuthenticationHandler>("ApiKey", _ => { });

// Bridge — auto-selected printer adapter
PrinterAdapterFactory.Register(services);  // WindowsPrinterAdapter or CupsPrinterAdapter
services.AddHttpClient(CloudBadgeClient.HttpClientName, ...);
```

## See also

- [Cloud API](cloud-api.md) — API endpoints, authentication, configuration
- [Bridge](bridge.md) — bridge endpoints, printer adapters, setup wizard, service install
- [Browser Pool](browser-pool.md) — how Chromium instances are pooled and recycled
- [Kafka Flow](kafka-flow.md) — Kafka-mode message flow and topic schema

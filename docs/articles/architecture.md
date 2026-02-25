# Architecture

## Solution structure

```
DocumentGenerator/
├── src/
│   ├── DocumentGenerator.Core          # Interfaces, models, configuration
│   ├── DocumentGenerator.Templating    # Handlebars engine + file resolver
│   ├── DocumentGenerator.Pdf           # Chromium pool + PDF renderer
│   ├── DocumentGenerator.Messaging     # Rebus/Kafka consumer + producer
│   └── DocumentGenerator.Console       # Host entry point, TUI, file-mode worker
├── tools/
│   └── DocumentGenerator.TestProducer  # Interactive Kafka test client
├── templates/                          # HTML, CSS, and JSON template files
├── tests/
│   ├── DocumentGenerator.UnitTests     # xUnit unit tests (89 tests)
│   └── DocumentGenerator.IntegrationTests  # Integration tests (18 tests)
└── docs/                               # This documentation (DocFX)
```

## Dependency graph

```
Console
  ├── Core          (models + interfaces — zero third-party deps)
  ├── Templating    → Core  (Handlebars.Net, QRCoder, ZXing.Net)
  ├── Pdf           → Core  (PuppeteerSharp)
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

## Render pipeline

```
JSON template file
    │
    ▼ DocumentGeneratorWorker (file mode) or DocumentRenderRequestHandler (Kafka)
    │  Deserialise → DocumentTemplate
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
services
    .AddTemplating()          // HandlebarsTemplateEngine + FileTemplateContentResolver
    .AddPdfRendering()        // ChromiumBrowserPool + PuppeteerDocumentRenderer + DocumentPipeline
    .AddRebusKafkaMessaging(kafkaOptions);  // Rebus, Kafka transport, handler
```

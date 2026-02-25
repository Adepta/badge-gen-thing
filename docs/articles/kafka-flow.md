# Kafka Flow

## Topic overview

| Topic | Direction | Partitions | Retention |
|---|---|---|---|
| `render.requests` | iPad → Generator | 4 | 24h |
| `render.results` | Generator → iPad | 4 | 1h |
| `render.deadletter` | Generator → Ops | 1 | 7d |

## End-to-end badge flow

```
iPad
 └─ Produces to render.requests
      Key:   CorrelationId (Guid)
      Value: DocumentRenderRequest (JSON)
             {
               correlationId, deviceId, sessionId,
               template: { documentType: "badge", ... }
             }

KafkaConsumerService
 └─ Polls render.requests (up to MaxConcurrentRenders parallel)
 └─ Deserialises → RenderRequest
 └─ IDocumentPipeline.ExecuteAsync()
      └─ HandlebarsTemplateEngine → HTML
      └─ ChromiumBrowserPool.AcquireAsync()
           └─ IBrowser.NewPageAsync() → Page
           └─ Page.SetContentAsync(html)
           └─ Page.PdfDataAsync() → byte[]
           └─ Page.CloseAsync()
      └─ Pool.ReturnAsync(browser)
 └─ On success → render.results
      Key:   CorrelationId
      Value: DocumentRenderResult (JSON)
             { correlationId, deviceId, success: true, pdfBase64, elapsedTime }
 └─ On failure (3 retries + exponential backoff) → render.deadletter
      Value: DocumentRenderResult (JSON)
             { correlationId, deviceId, success: false, errorMessage }

iPad
 └─ Consumes render.results
 └─ Filters by CorrelationId / DeviceId
 └─ Decodes pdfBase64 → hands to AirPrint / badge printer
```

## Message headers

Every message on `render.results` and `render.deadletter` carries Kafka headers for lightweight filtering without deserialising the payload:

| Header | Value |
|---|---|
| `deviceId` | The originating iPad device ID |
| `documentType` | e.g. `badge`, `invoice` |
| `success` | `True` or `False` |

## Retry policy

Failed renders are retried up to `MaxRetries` times (default 3) with exponential backoff:

| Attempt | Delay |
|---|---|
| 1 | 2s |
| 2 | 4s |
| 3 | 8s |
| → dead-letter | — |

## Consumer group scaling

All instances of DocumentGenerator share the same `ConsumerGroupId` (`document-generator`). Kafka distributes the 4 partitions of `render.requests` across all running instances automatically — giving horizontal scale without duplicate processing.

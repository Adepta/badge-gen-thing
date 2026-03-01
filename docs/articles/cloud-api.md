# Cloud API — DocumentGenerator.Api

`DocumentGenerator.Api` is the cloud-hosted ASP.NET Core Web API that receives badge render requests from bridge services and returns Base64-encoded PDF or PNG documents.

---

## Deployment model

```
iPad (local network)
    │
    ▼
DocumentGenerator.Bridge  ──── X-Api-Key ────► DocumentGenerator.Api  (cloud / server)
    │                                                   │
    │ Base64 PDF                                        ▼
    │◄──────────────────────────────────────  Chromium renders badge
    │
    ▼
Local printer (OS spooler)
```

The API is stateless and scales horizontally. Every request is self-contained: the template name tells the API which HTML/CSS files to use, and the variables/branding fields provide the attendee data.

---

## Authentication

All endpoints except `GET /health` require an **`X-Api-Key`** header.

```
X-Api-Key: your-api-key-here
```

The key is configured in `appsettings.json` under `ApiAuth:ApiKey` and is intended to be overridden via an environment variable in production:

```bash
export ApiAuth__ApiKey="my-secret-production-key"
```

Missing or incorrect keys return **401 Unauthorized**.

---

## Endpoints

### `GET /health`

Liveness probe. No authentication required.

**Response 200:**
```json
{ "status": "healthy", "utc": "2026-01-01T12:00:00Z" }
```

---

### `GET /api/badges/templates`

Returns all template names available on the server.

**Headers:** `X-Api-Key: <key>`

**Response 200:**
```json
["badge-carbon-a6", "badge-carbon-cc", "badge-executive-a6", "badge-executive-cc", "badge-pulse-a6", "badge-pulse-cc"]
```

Templates are discovered by scanning the configured `DocumentGenerator:TemplatesPath` directory for `*.html` files. Files prefixed with `sample-` are excluded from the list.

---

### `POST /api/badges/render`

Renders a badge and returns the result as a Base64-encoded document.

**Headers:** `X-Api-Key: <key>`, `Content-Type: application/json`

**Request body:**

```json
{
  "templateName":  "badge-pulse-a6",
  "variables": {
    "firstName":   "Jane",
    "lastName":    "Smith",
    "jobTitle":    "Principal Engineer",
    "company":     "Acme Corp",
    "ticketType":  "Speaker",
    "attendeeId":  "ATT-001"
  },
  "branding": {
    "companyName":     "Acme Corp",
    "primaryColour":   "#1e3a5f",
    "secondaryColour": "#c8a84b"
  },
  "format":        "Pdf",
  "correlationId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
}
```

| Field | Type | Required | Description |
|---|---|---|---|
| `templateName` | string | Yes | Template identifier, e.g. `"badge-pulse-a6"` |
| `variables` | object | Yes | Key-value pairs injected as `{{variables.*}}` |
| `branding` | object | No | Per-request branding overrides |
| `format` | string | No | `"Pdf"` (default) or `"Png"` |
| `correlationId` | GUID | No | Echoed in the response; auto-generated if omitted |

**Response 200:**
```json
{
  "correlationId":  "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "jobId":          "d290f1ee-6c54-4b01-90e6-d701748f0851",
  "success":        true,
  "documentBase64": "JVBERi0xLjQ...",
  "mimeType":       "application/pdf",
  "documentType":   "badge",
  "elapsedTime":    "00:00:00.823",
  "completedAt":    "2026-01-01T12:00:00.823Z",
  "error":          null
}
```

**Response 400** — unknown template name or validation failure:
```json
{
  "correlationId": "...",
  "success":       false,
  "error":         "Badge template 'badge-unknown' not found."
}
```

**Response 401** — missing or invalid API key.

**Response 500** — unexpected rendering failure (Chromium crash, etc.).

---

## Configuration

**`src/DocumentGenerator.Api/appsettings.json`:**

```json
{
  "ApiAuth": {
    "ApiKey": "CHANGE-ME-IN-PRODUCTION"
  },

  "BrowserPool": {
    "MinSize":               1,
    "MaxSize":               4,
    "AcquireTimeout":        "00:00:30",
    "IdleTimeout":           "00:05:00",
    "MaxRendersPerInstance": 100
  },

  "DocumentGenerator": {
    "TemplatesPath": "templates"
  }
}
```

| Key | Default | Description |
|---|---|---|
| `ApiAuth:ApiKey` | `"CHANGE-ME-IN-PRODUCTION"` | API key required in `X-Api-Key` header |
| `BrowserPool:MinSize` | `1` | Minimum warm Chromium instances kept alive |
| `BrowserPool:MaxSize` | `4` | Maximum concurrent Chromium instances |
| `BrowserPool:AcquireTimeout` | `00:00:30` | How long to wait for a free browser before failing |
| `BrowserPool:MaxRendersPerInstance` | `100` | Instances are recycled after this many renders |
| `DocumentGenerator:TemplatesPath` | `"templates"` | Directory scanned for `*.html` template files |

---

## Templates

The API project links templates from `DocumentGenerator.Console/templates/` via MSBuild `Include` items with `LinkBase`, so the same HTML/CSS files are used whether you run the console worker locally or call the cloud API.

See [Badge Designs](badge-designs.md) for a description of each included template and [Template Schema](template-schema.md) for the full field reference.

---

## Running locally

```bash
dotnet run --project src/DocumentGenerator.Api
```

The API listens on:
- `https://localhost:7070`
- `http://localhost:7071`

Test with curl:

```bash
curl -s -X POST http://localhost:7071/api/badges/render \
  -H "Content-Type: application/json" \
  -H "X-Api-Key: CHANGE-ME-IN-PRODUCTION" \
  -d '{
    "templateName": "badge-pulse-a6",
    "variables": { "firstName": "Jane", "lastName": "Smith", "jobTitle": "Engineer", "company": "Acme" }
  }' | jq .success
```

---

## DI wiring

`Program.cs` reuses the same extension methods as the console worker:

```csharp
services
    .AddTemplating()     // HandlebarsTemplateEngine + FileTemplateContentResolver
    .AddPdfRendering();  // ChromiumBrowserPool + PuppeteerDocumentRenderer + DocumentPipeline
```

`TemplateLocator` is registered as a singleton and reads `DocumentGenerator:TemplatesPath` from configuration to discover templates at startup.

`ApiKeyAuthenticationHandler` is registered as a custom `AuthenticationScheme` and applied globally via `[Authorize]` on `BadgesController`.

---

## See also

- [Bridge](bridge.md) — client-side service that calls this API and forwards to the local printer
- [Architecture](architecture.md) — end-to-end flow including iPad → Bridge → Cloud API
- [Browser Pool](browser-pool.md) — how Chromium instances are pooled and recycled

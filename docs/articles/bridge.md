# Bridge — DocumentGenerator.Bridge

`DocumentGenerator.Bridge` is a lightweight cross-platform HTTP service that runs on a PC or server **on the same local network as the iPad**. It acts as a proxy between the iPad and the cloud API, and handles local printer spooling.

---

## Why a bridge?

iPads cannot talk directly to local network printers, and sending raw print jobs from the cloud would require a printer to be publicly reachable. The bridge solves both problems:

1. The iPad sends a simple HTTP request to the bridge (local network — no auth).
2. The bridge authenticates to the cloud API and retrieves the rendered Base64 PDF.
3. The bridge decodes the PDF and submits it to the local OS print spooler.
4. The bridge returns the Base64 document to the iPad so it can display a preview.

---

## Request flow

```
iPad
  │  POST /print  { templateName, variables }
  ▼
DocumentGenerator.Bridge  (local network, port 5100)
  │
  ├─► POST /api/badges/render  (cloud, X-Api-Key)
  │       ▼  { documentBase64, mimeType }
  │
  ├─► OS print spooler (Windows: System.Drawing.Printing  |  Linux/macOS: CUPS lp)
  │       Printer: <configured default or request override>
  │
  └─► 200 OK  { success, documentBase64, mimeType, printed, printerUsed, … }
  ▼
iPad  (displays preview or confirmation)
```

---

## Endpoints

### `GET /health`

Liveness probe. No authentication. Always returns 200.

**Response:**
```json
{ "status": "healthy", "isConfigured": true, "utc": "2026-01-01T12:00:00Z" }
```

---

### `GET /printers`

Lists locally installed printers.

**Response 200:**
```json
["Microsoft Print to PDF", "HP LaserJet M404dn", "Zebra ZD421"]
```

---

### `GET /templates`

Proxies the cloud API's template list.

**Response 200:**
```json
["badge-carbon-a6", "badge-carbon-cc", "badge-executive-a6", "badge-executive-cc", "badge-pulse-a6", "badge-pulse-cc"]
```

---

### `POST /render`

Renders a badge via the cloud API and returns the Base64 document. **Does not print.**

**Request body:**
```json
{
  "templateName": "badge-pulse-a6",
  "variables": {
    "firstName": "Jane",
    "lastName":  "Smith",
    "jobTitle":  "Engineer",
    "company":   "Acme Corp"
  },
  "correlationId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
}
```

**Response 200:**
```json
{
  "correlationId":  "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "success":        true,
  "documentBase64": "JVBERi0xLjQ...",
  "mimeType":       "application/pdf",
  "printed":        null,
  "printerUsed":    null,
  "elapsedTime":    "00:00:00.912",
  "completedAt":    "2026-01-01T12:00:00.912Z"
}
```

---

### `POST /print`

Renders a badge via the cloud API **and** sends it to the local printer.

**Request body** — same as `/render`, with an optional printer override:

```json
{
  "templateName": "badge-pulse-a6",
  "variables":    { "firstName": "Jane", "lastName": "Smith", "jobTitle": "Engineer", "company": "Acme" },
  "printerName":  "Zebra ZD421"
}
```

| Field | Type | Required | Description |
|---|---|---|---|
| `templateName` | string | Yes | Template identifier |
| `variables` | object | Yes | Attendee data injected as `{{variables.*}}` |
| `branding` | object | No | Per-request branding overrides forwarded to cloud |
| `printerName` | string | No | Overrides `Printer:DefaultPrinterName`; uses OS default when null |
| `correlationId` | GUID | No | Echoed in response; auto-generated if omitted |

**Response 200 (success):**
```json
{
  "correlationId":  "...",
  "success":        true,
  "documentBase64": "JVBERi0xLjQ...",
  "mimeType":       "application/pdf",
  "printed":        true,
  "printerUsed":    "Zebra ZD421",
  "elapsedTime":    "00:00:01.241",
  "completedAt":    "2026-01-01T12:00:01.241Z"
}
```

**Response 200 (print failed — document still returned):**

When the cloud renders successfully but the local printer fails, `success` is `false` and `printed` is `false`, but `documentBase64` is still present so the iPad can display a preview:

```json
{
  "success":        false,
  "documentBase64": "JVBERi0xLjQ...",
  "printed":        false,
  "error":          "Print spooler error: Printer offline"
}
```

---

## First-run setup wizard

When `Bridge:IsConfigured` is `false`, the `SetupGuardMiddleware` redirects all non-health requests to `/setup`, which serves a dark-themed browser UI.

The wizard collects:
- Cloud API base URL and API key
- Default local printer name
- Document format (`Pdf` or `Png`)
- Listen port (default: 5100)

On submission, `SetupService` writes the values to `appsettings.json` and sets `Bridge:IsConfigured = true`. **Restart the bridge** to apply the new settings.

### Setup endpoints

| Method | Route | Description |
|---|---|---|
| `GET` | `/setup` | Serves the setup wizard HTML page |
| `GET` | `/setup/printers` | Lists local printers (AJAX) |
| `POST` | `/setup/test-connection` | Tests cloud API reachability (AJAX) |
| `POST` | `/setup/save` | Writes config and marks bridge as configured |

---

## Configuration

**`src/DocumentGenerator.Bridge/appsettings.json`:**

```json
{
  "Bridge": {
    "port":         5100,
    "isConfigured": false
  },
  "Cloud": {
    "baseUrl": "https://your-api.example.com",
    "apiKey":  "your-api-key",
    "timeout": "00:00:30"
  },
  "Printer": {
    "defaultPrinterName": null,
    "format":             "Pdf"
  }
}
```

| Key | Default | Description |
|---|---|---|
| `Bridge:Port` | `5100` | HTTP listen port |
| `Bridge:IsConfigured` | `false` | Set to `true` by setup wizard; guards normal endpoints |
| `Cloud:BaseUrl` | `""` | Base URL of the cloud `DocumentGenerator.Api` |
| `Cloud:ApiKey` | `""` | API key sent in the `X-Api-Key` header |
| `Cloud:Timeout` | `00:00:30` | HTTP timeout for cloud requests |
| `Printer:DefaultPrinterName` | `null` | Default printer; `null` uses the OS default |
| `Printer:Format` | `"Pdf"` | `"Pdf"` or `"Png"` |

---

## Printer adapters

The bridge uses a platform-detected adapter pattern to abstract OS printer APIs.

| Platform | Adapter | Mechanism |
|---|---|---|
| Windows | `WindowsPrinterAdapter` | `System.Diagnostics.Process` — `ShellExecute` with `"printto"` verb |
| Linux / macOS | `CupsPrinterAdapter` | `lp` command via `System.Diagnostics.Process` |

`PrinterAdapterFactory` selects the correct adapter at startup using `RuntimeInformation.IsOSPlatform`. The `IPrinterAdapter` interface makes both adapters fully mockable in tests.

---

## Running locally

```bash
dotnet run --project src/DocumentGenerator.Bridge
```

The bridge listens on `http://+:5100` by default. On first run, navigate to [http://localhost:5100/setup](http://localhost:5100/setup) to complete configuration.

---

## Installing as a system service

### Windows (Service)

```powershell
# Publish a self-contained exe
dotnet publish src/DocumentGenerator.Bridge -c Release -r win-x64 --self-contained

# Install as a Windows Service
sc create DocumentGeneratorBridge binPath="C:\path\to\publish\DocumentGenerator.Bridge.exe"
sc start DocumentGeneratorBridge
```

### Linux (systemd)

```bash
# Publish
dotnet publish src/DocumentGenerator.Bridge -c Release -r linux-x64 --self-contained

# Create unit file at /etc/systemd/system/docgen-bridge.service
[Unit]
Description=DocumentGenerator Bridge

[Service]
ExecStart=/opt/docgen-bridge/DocumentGenerator.Bridge
WorkingDirectory=/opt/docgen-bridge
Restart=always

[Install]
WantedBy=multi-user.target

# Enable and start
systemctl enable --now docgen-bridge
```

The bridge uses `UseWindowsService()` + `UseSystemd()` from the ASP.NET Core hosting extensions, so it handles both supervisors natively without any additional code.

---

## See also

- [Cloud API](cloud-api.md) — the server-side rendering API the bridge calls
- [Architecture](architecture.md) — full end-to-end flow
- [Testing](testing.md) — bridge integration test coverage

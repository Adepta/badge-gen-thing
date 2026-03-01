# Error Codes

All deliberate errors in the DocumentGenerator pipeline are represented by a typed exception that
derives from `DocumentGeneratorException` (in `DocumentGenerator.Core.Errors`). Every exception
carries a machine-readable `ErrorCode` enum value formatted as `DG` + 4 digits.

Error codes are surfaced in three places:

| Surface | Field |
|---------|-------|
| API HTTP response body (`BadgeRenderResponse`) | `errorCode` (e.g. `"DG1001"`) |
| Kafka result message (`DocumentRenderResult`) | `errorCode` |
| Bridge HTTP response body (`PrintResponse`) | `errorCode` |
| Structured log entries | `[DG1001]` prefix in the log message |

---

## Code Ranges

| Range | Category | Exception class |
|-------|----------|-----------------|
| DG1xx | Template errors | `TemplateException` |
| DG2xx | Render / pipeline errors | `RenderException` |
| DG3xx | Browser pool errors | `BrowserPoolException` |
| DG4xx | Messaging / broker errors | `BrokerException` |
| DG5xx | Printing / bridge errors | `PrintException` |
| DG6xx | Configuration errors | `ConfigurationException` |
| DG9xx | Unexpected / unclassified | `DocumentGeneratorException` |

---

## DG1xx — Template Errors

### DG1001 — TemplateNotFound

**Class:** `TemplateException`  
**Thrown by:** `TemplateLocator.Resolve()`  
**HTTP mapping:** `400 Bad Request`

The requested template name does not exist in the configured templates directory
(`DocumentGenerator:TemplatesPath`).

**Resolution:** Check the template name is spelled correctly and that the corresponding
`.html` file exists on the API host.

---

### DG1002 — TemplateReadFailed

**Class:** `TemplateException`  
**Thrown by:** `FileTemplateContentResolver.ResolveAsync()`  
**HTTP mapping:** `500 Internal Server Error`

The template HTML or CSS file exists on disk but could not be read. Common causes:
file permissions, file locked by another process, or disk error.

**Resolution:** Check file permissions on the templates directory and ensure no other
process holds an exclusive lock on the file.

---

### DG1003 — TemplateNameInvalid

**Class:** `TemplateException`  
**Thrown by:** `TemplateLocator.Resolve()`  
**HTTP mapping:** `400 Bad Request`

The template name supplied by the caller is null, empty, or contains invalid path characters.

**Resolution:** Validate template names on the client before sending requests. Template names
must be non-empty strings containing only characters valid in a filename (no slashes, dots,
or null bytes).

---

### DG1004 — TemplateCompileFailed

**Class:** `TemplateException`  
**Thrown by:** `HandlebarsTemplateEngine`  
**HTTP mapping:** `500 Internal Server Error`

The Handlebars template contains a syntax error and could not be compiled. This is a
template-authoring error, not a runtime data problem.

**Resolution:** Validate the `.html` template file with a Handlebars linter. Common causes:
unclosed `{{` blocks, invalid helper names, or malformed partials.

---

### DG1005 — TemplateRenderFailed

**Class:** `TemplateException`  
**Thrown by:** `HandlebarsTemplateEngine`  
**HTTP mapping:** `500 Internal Server Error`

Handlebars rendering failed at runtime. Common causes: a required helper threw an exception,
a partial referenced in the template is not registered, or a variable access threw.

**Resolution:** Check the Handlebars template for helpers that may throw on certain input
values (e.g. `{{currency}}` with a non-numeric value).

---

## DG2xx — Render / Pipeline Errors

### DG2001 — PipelineFailed

**Class:** `RenderException`  
**Thrown by:** `DocumentPipeline.ExecuteAsync()`  
**HTTP mapping:** `500 Internal Server Error`  
**Kafka result:** `success: false, errorCode: "DG2001"`

An unexpected error occurred during the render pipeline. This wraps errors not covered by
more specific codes (DG1xxx / DG3xxx). The inner exception is logged with full stack trace.

**Resolution:** Check the structured logs for the `CorrelationId` to find the inner exception
and stack trace. Common causes: Chromium crash, out-of-memory, or disk full.

---

### DG2002 — RenderEmptyOutput

**Class:** `RenderException`  
**Thrown by:** `DocumentPipeline.ExecuteAsync()`  
**HTTP mapping:** `500 Internal Server Error`

Chromium completed PDF generation but returned zero bytes. This typically indicates the page
failed to load correctly (blank HTML, missing CSS, network resource timeout).

**Resolution:** Test the template manually in a browser. Ensure all external resources
(Google Fonts, logo images) are reachable from the render host.

---

### DG2003 — RenderCancelled

**Class:** `OperationCanceledException` (not wrapped)  
**Thrown by:** Any pipeline stage  
**HTTP mapping:** `499 Client Closed Request`

The render was cancelled, either because the HTTP client disconnected or because an explicit
`CancellationToken` was triggered.

**Resolution:** No action required — the client abandoned the request. This is not an error
condition; it is informational.

---

### DG2004 — RenderPageTimeout

**Class:** `RenderException`  
**Thrown by:** `PuppeteerDocumentRenderer.RenderPdfAsync()`  
**HTTP mapping:** `500 Internal Server Error`

Chromium timed out loading the HTML page before the configured navigation timeout
(`30_000 ms` by default). External resources that never respond are the most common cause.

**Resolution:**
- Remove or replace slow external resources (use locally hosted fonts / images where possible).
- If external resources are required, increase the navigation timeout via
  `BrowserPool:NavigationTimeoutMs` (see open issue — this is currently hard-coded).

---

## DG3xx — Browser Pool Errors

### DG3001 — BrowserPoolTimeout

**Class:** `BrowserPoolException`  
**Thrown by:** `ChromiumBrowserPool.AcquireAsync()`  
**HTTP mapping:** `500 Internal Server Error`

All Chromium instances in the pool are busy and no browser became available within the
configured `BrowserPool:AcquireTimeout` (default `30s`).

**Resolution:**
- Increase `BrowserPool:MaxSize` and `Kafka:MaxConcurrentRenders` to match available CPU/memory.
- Increase `BrowserPool:AcquireTimeout` if renders are temporarily slow.
- Add more Console worker replicas to distribute the load.

---

### DG3002 — BrowserLaunchFailed

**Class:** `BrowserPoolException`  
**Thrown by:** `ChromiumBrowserPool` (internal `LaunchBrowserAsync`)  
**HTTP mapping:** `500 Internal Server Error`

Chromium could not be launched. Common causes on Linux: missing sandbox permissions,
missing system libraries, or Chromium binary not downloaded.

**Resolution:**
- Ensure `BrowserFetcher().DownloadAsync()` completes successfully at startup (check Console logs).
- On Linux, verify that `--no-sandbox` is being passed (it is by default) and that required
  system libraries (`libnss3`, `libatk`, etc.) are installed.
- Check the process has execute permissions on the Chromium binary.

---

### DG3003 — BrowserDisconnected

**Class:** `BrowserPoolException`  
**Thrown by:** `PuppeteerDocumentRenderer`  
**HTTP mapping:** `500 Internal Server Error`

A Chromium instance disconnected unexpectedly during a render. The browser was killed by the
OS (OOM, signal), crashed internally, or the process was externally terminated.

**Resolution:**
- Check system memory — OOM-killer terminating Chromium is the most common cause.
- Review `--disable-dev-shm-usage` is set (it is by default) in containerised environments.
- Check `MaxRendersPerInstance` — reduce it if browser memory usage grows over time.

---

### DG3004 — BrowserPoolDisposed

**Class:** `BrowserPoolException`  
**Thrown by:** `ChromiumBrowserPool.AcquireAsync()`  
**HTTP mapping:** `500 Internal Server Error`

The browser pool has been shut down and can no longer accept new render requests. This
occurs if a render is attempted after the host receives a graceful shutdown signal.

**Resolution:** Restart the service. In normal operation this should not be observed unless
a request arrives during the shutdown window.

---

## DG4xx — Messaging / Broker Errors

### DG4001 — BrokerPublishFailed

**Class:** `BrokerException`  
**Thrown by:** `BadgesController.RenderViaKafkaAsync()`  
**HTTP mapping:** `500 Internal Server Error`

The API failed to publish a `DocumentRenderRequest` to the `render.requests` Kafka topic.
Common causes: broker unreachable, authentication failure, or topic does not exist.

**Resolution:**
- Verify `Kafka:BootstrapServers` points to a reachable broker.
- Ensure the `render.requests` topic exists and the API principal has produce permissions.
- Check Kafka broker logs for authentication errors if SASL is configured.

---

### DG4002 — BrokerResultTimeout

**Class:** `BrokerException`  
**Thrown by:** `BadgesController.RenderViaKafkaAsync()` (via `PendingRenderStore`)  
**HTTP mapping:** `504 Gateway Timeout`

The API published the render request to Kafka but did not receive a result from the Console
worker within `Kafka:ResultTimeoutSeconds` (default `25s`).

**Resolution:**
- Check that at least one Console worker is running and connected to Kafka.
- Check Console logs for `DG2xxx` or `DG3xxx` errors that may indicate the render is failing.
- If renders are genuinely slow, increase `Kafka:ResultTimeoutSeconds` on the API and
  `Cloud:Timeout` on the Bridge accordingly.

---

### DG4003 — BrokerDeserializeFailed

**Class:** `BrokerException` (reserved — not yet thrown)  
**Thrown by:** Kafka transport layer  

A Kafka message could not be deserialized into the expected message type. This indicates a
schema mismatch between producer and consumer versions.

**Resolution:** Ensure the API and Console are running the same version of
`DocumentGenerator.Messaging` so the message contracts (`DocumentRenderRequest`,
`DocumentRenderResult`) are identical.

---

### DG4004 — BrokerDeadLettered

**Class:** `BrokerException` (reserved — Rebus handles this internally)  
**Topic:** `render.deadletter`  

A render request exceeded `Kafka:MaxRetries` attempts (default `3`) and was moved to the
dead-letter topic.

**Resolution:**
- Consume messages from `render.deadletter` to inspect the failed requests.
- Check Console logs for `DG2xxx` or `DG3xxx` errors for the corresponding `CorrelationId`.
- Fix the underlying render failure before re-processing.

---

## DG5xx — Printing / Bridge Errors

### DG5001 — CloudRenderFailed

**Class:** `PrintException`  
**Thrown by:** `CloudBadgeClient.RenderAsync()`  
**Bridge response:** `success: false, errorCode: "DG5001"`

The Bridge could not obtain a rendered document from the cloud API. This covers network
errors and non-2xx HTTP responses from the API.

**Resolution:**
- Verify `Cloud:BaseUrl` is correct and the API is reachable from the Bridge host.
- Check `Cloud:ApiKey` matches `ApiAuth:ApiKey` on the API.
- Inspect the cloud API logs for the corresponding `CorrelationId`.

---

### DG5002 — CloudResponseDecodeFailed

**Class:** `PrintException`  
**Thrown by:** `BridgeEndpoints` (`POST /print`)  
**Bridge response:** `success: false, errorCode: "DG5002"`

The Base64 document payload returned by the cloud API could not be decoded to bytes. This
indicates a corrupted or truncated response body.

**Resolution:** Check for proxy/gateway truncation between the Bridge and cloud API.
Verify the cloud API response `Content-Length` matches the body received.

---

### DG5003 — PrintSpoolerFailed

**Class:** `PrintException`  
**Thrown by:** `WindowsPrinterAdapter` / `CupsPrinterAdapter`  
**Bridge response:** `success: false, printed: false, errorCode: "DG5003"`  
**Note:** The document is still returned to the iPad even when printing fails.

The local print spooler rejected or could not process the job. The rendered document is
still returned in the response so the iPad can display a preview.

**Resolution:**
- Check the printer is online and not in an error state.
- Verify the printer name matches the output of `GET /printers` on the Bridge.
- On Windows, check the Windows Event Log under `Applications and Services Logs > PrintService`.

---

### DG5004 — PrintProcessTimeout

**Class:** `PrintException`  
**Thrown by:** `WindowsPrinterAdapter`  
**Bridge response:** `success: false, printed: false, errorCode: "DG5004"`

The print helper process (SumatraPDF or Edge) did not exit within `30_000 ms`. The process
was killed.

**Resolution:**
- Verify SumatraPDF is installed and the binary path is correct (or configure
  `Printer:SumatraPdfPath`).
- Check for printer driver hangs — try printing a test page from the OS.

---

### DG5005 — PrintHelperNotFound

**Class:** `PrintException`  
**Thrown by:** `WindowsPrinterAdapter`  
**Bridge response:** `success: false, printed: false, errorCode: "DG5005"`

No suitable PDF print helper (SumatraPDF, Microsoft Edge, Acrobat) was found on the local
machine and the fallback `ShellExecute` also failed.

**Resolution:** Install [SumatraPDF](https://www.sumatrapdfreader.org/free-pdf-reader) on
the Bridge host, or configure an explicit path via `Printer:SumatraPdfPath` in
`appsettings.json`.

---

## DG6xx — Configuration Errors

### DG6001 — ConfigurationMissing

**Class:** `ConfigurationException`  
**Thrown by:** Startup validation  

A required configuration key is absent or empty. The key name is included in the exception
`Context` dictionary and in the log message.

**Resolution:** Add the missing key to `appsettings.json` or as an environment variable.
Refer to `.env.example` for all required keys.

---

### DG6002 — ConfigurationInvalid

**Class:** `ConfigurationException`  
**Thrown by:** Startup validation  

A configuration value is present but invalid (wrong type, out of range, invalid URL format,
etc.). The key name and reason are included in the exception message.

**Resolution:** Correct the value in `appsettings.json` or the corresponding environment
variable. See the exception message for the specific constraint violated.

---

## DG9xx — Unexpected Errors

### DG9001 — Unexpected

**Class:** `DocumentGeneratorException`  
**Thrown by:** `DocumentRenderRequestHandler` catch-all  
**Kafka result:** `success: false, errorCode: "DG9001"`

An unexpected exception occurred that does not match any known error category. The full
stack trace is logged at `Error` level.

**Resolution:** Check the structured logs for the `CorrelationId`. This code indicates a
bug or environmental problem not anticipated by the domain model — raise an issue with the
full log output.

---

## Adding a New Error Code

1. Add the value to `ErrorCode.cs` in `DocumentGenerator.Core/Errors/` following the range conventions.
2. Add a factory method to the appropriate typed exception class (or create a new one that
   extends `DocumentGeneratorException`).
3. Throw the typed exception from the relevant service layer.
4. Add a row to this document.

# Testing

DocumentGenerator has a two-project test suite totalling **109 test executions** across 103 test methods.

| Project | Tests | Type | Chromium? |
|---|---|---|---|
| `DocumentGenerator.UnitTests` | 91 executions (85 methods) | Unit | No |
| `DocumentGenerator.IntegrationTests` | 18 | Integration | No |

All tests run fully in-process. No real browser, no Kafka broker, and no file system state outside of explicitly-created temp directories is required.

---

## Running the Tests

### All tests

```bash
dotnet test
```

### One project at a time

```bash
dotnet test tests/DocumentGenerator.UnitTests
dotnet test tests/DocumentGenerator.IntegrationTests
```

### With detailed output

```bash
dotnet test --logger "console;verbosity=detailed"
```

### A single test class

```bash
dotnet test --filter "FullyQualifiedName~HandlebarsTemplateEngineTests"
```

---

## Unit Tests — `DocumentGenerator.UnitTests`

Every class is `sealed` and independent. Tests use constructor setup (xUnit's constructor/dispose pattern) so shared fields are re-initialised for each test. Mocks are created with **Moq** and assertions use **FluentAssertions**.

### `Core.ModelTests` (20 tests)

**File:** `tests/DocumentGenerator.UnitTests/Core/ModelTests.cs`

Pure in-memory tests. No mocks, no I/O. Covers the value-object and factory methods on the core models:

| Coverage area | Tests |
|---|---|
| `RenderResult.Success()` factory | Sets `JobId`, `PdfBytes`, `ElapsedTime`, `DocumentType` |
| `DocumentRenderResult.Succeeded()` factory | `Success=true`, Base64-encoded `PdfBase64`, null `ErrorMessage`, echoed correlation/device/session IDs |
| `DocumentRenderResult.Failed()` factory | `Success=false`, `ErrorMessage` set, `PdfBase64` null |
| `RenderRequest` defaults | Auto-generated unique `JobId`, `CreatedAt ≈ UtcNow` |
| `DocumentTemplate` defaults | `Version="1.0"`, non-null empty `Variables`, `Pdf.Format="A4"` |
| `PdfOptions` defaults | Format, Landscape, PrintBackground, Scale, Width, Height, Margins |
| `Branding` defaults | Non-null empty `Custom` dictionary |
| `TemplateContent` defaults | Non-null empty `Partials`, null `Css` |

### `Messaging.DocumentRenderRequestHandlerTests` (11 tests)

**File:** `tests/DocumentGenerator.UnitTests/Messaging/DocumentRenderRequestHandlerTests.cs`

Mocks: `Mock<IDocumentPipeline>`, `Mock<IBus>`, `Mock<IRenderMetrics>`

Tests the Kafka message handler in isolation:

| Scenario | Assertions |
|---|---|
| Successful render | `IBus.Reply` called once with `Success=true`; PDF bytes Base64-encoded; correlation/device/session IDs echoed; `IRenderMetrics.RecordSuccess()` called once |
| Successful render | Pipeline invoked with a `RenderRequest` whose `JobId` matches the message's `CorrelationId` |
| Pipeline throws | `IBus.Reply` called with `Success=false`; exception message in `ErrorMessage`; `IRenderMetrics.RecordFailure()` called once; bus reply is never swallowed |

### `Pdf.DocumentPipelineTests` (10 tests)

**File:** `tests/DocumentGenerator.UnitTests/Pdf/DocumentPipelineTests.cs`

Mocks: `Mock<ITemplateEngine>`, `Mock<IDocumentRenderer>`

Tests the two-step pipeline (`ITemplateEngine` → `IDocumentRenderer`) in isolation:

| Scenario | Assertions |
|---|---|
| Happy path | Returns PDF bytes; `JobId` and `DocumentType` preserved in result; `ElapsedTime > Zero` |
| Happy path | `ITemplateEngine.RenderAsync` called with the correct template; renderer called with the HTML from the engine and the correct `PdfOptions` |
| Engine throws | Exception propagates; renderer never called |
| Renderer throws | Exception propagates |

### `Templating.FileTemplateContentResolverTests` (14 tests)

**File:** `tests/DocumentGenerator.UnitTests/Templating/FileTemplateContentResolverTests.cs`

Uses `IDisposable` — creates a unique temp directory per test run and deletes it in `Dispose()`. No mocks; tests the real `FileTemplateContentResolver` against real temp files.

| Scenario | Assertions |
|---|---|
| No paths set | Same template instance returned; inline HTML preserved |
| `HtmlPath` only (relative) | Resolved against `basePath`; file content loaded into `Html`; `Css` remains null |
| `HtmlPath` only (absolute) | Used as-is regardless of `basePath` |
| `CssPath` only | File content loaded into `Css`; `Html` defaults to `string.Empty` |
| Both paths set | Both files loaded; `HtmlPath`/`CssPath` strings preserved on result; new instance returned |
| Non-template properties | `DocumentType`, `Version`, `Branding`, `Variables`, `Pdf` preserved on resolved result |
| Cancelled token | `OperationCanceledException` thrown |
| File not found (`HtmlPath`) | `FileNotFoundException` thrown |
| File not found (`CssPath`) | `FileNotFoundException` thrown |

### `Templating.HandlebarsTemplateEngineTests` (36 executions / 30 methods)

**File:** `tests/DocumentGenerator.UnitTests/Templating/HandlebarsTemplateEngineTests.cs`

No mocks, no I/O. Tests the real `HandlebarsTemplateEngine` with in-memory templates. Two methods are `[Theory]` with 3 data rows each, giving 36 total executions.

**Variable and branding substitution**

| Test | What's verified |
|---|---|
| `RenderAsync_SimpleLiteral_ReturnsHtmlUnchanged` | Plain HTML passes through unchanged |
| `RenderAsync_VariableSubstitution_InjectsValue` | `{{variables.X}}` resolved from `Variables` dict |
| `RenderAsync_BrandingSubstitution_InjectsCompanyName` | `{{branding.companyName}}` resolved from `Branding` |
| `RenderAsync_MetaFields_ArePresent` | `{{meta.documentType}}` and `{{meta.version}}` populated |
| `RenderAsync_MetaGeneratedAt_IsIso8601` | `{{meta.generatedAt}}` is a valid ISO-8601 timestamp |
| `RenderAsync_BrandingCustomKey_IsAccessible` | `{{branding.custom.KEY}}` accessible from `Branding.Custom` dict |

**CSS injection**

| Test | What's verified |
|---|---|
| `RenderAsync_WithCssAndHeadTag_InjectsCssBeforeCloseHead` | `<style>…</style>` injected before `</head>` |
| `RenderAsync_WithCssButNoHeadTag_PrependsCssStyle` | `<style>` block prepended when no `<head>` |
| `RenderAsync_WithNoCss_HtmlNotModified` | No modification when `Css` is null/empty |
| `RenderAsync_CssWithHandlebarsVariable_SubstitutesBrandColour` | Handlebars in CSS resolved (e.g. `{{branding.primaryColour}}`) |
| `RenderAsync_CssWithTripleBrace_DoesNotThrow` | CSS with `{}` characters does not confuse the parser |

**Built-in helpers**

| Test | Helper | What's verified |
|---|---|---|
| `Helper_Upper_ConvertsToUpperCase` × 3 | `{{upper}}` | "hello"→"HELLO", "World"→"WORLD", ""→"" |
| `Helper_Lower_ConvertsToLowerCase` × 3 | `{{lower}}` | "HELLO"→"hello", "World"→"world", ""→"" |
| `Helper_FormatDate_FormatsWithSuppliedFormat` | `{{formatDate}}` | Date formatted with supplied pattern |
| `Helper_FormatDate_InvalidInput_RendersEmpty` | `{{formatDate}}` | Invalid date input renders empty string |
| `Helper_Currency_FormatsDecimalWithCulture` | `{{currency}}` | Numeric string formatted with culture code (e.g. `"en-GB"` → `£9.99`) |
| `Helper_IfEquals_RendersTemplateWhenEqual` | `{{#ifEquals}}` | Body rendered when values match |
| `Helper_IfEquals_RendersInverseWhenNotEqual` | `{{#ifEquals}}` | `{{else}}` rendered when values differ |

**QR code helper**

| Test | What's verified |
|---|---|
| `Helper_QrCode_EmitsSvgElement` | Non-empty value produces an SVG element |
| `Helper_QrCode_EmptyValue_EmitsNothing` | Empty string produces no output |
| `Helper_QrCode_NullValue_EmitsNothing` | Null value produces no output |
| `Helper_QrCode_CustomDarkColour_AppearsInSvg` | Custom dark-colour parameter reflected in SVG |
| `Helper_QrCode_TransparentLight_NoWhiteFill` | `"transparent"` light colour produces no white-fill attribute |

**Barcode helper**

| Test | What's verified |
|---|---|
| `Helper_BarCode_EmitsSvgElement` | Non-empty value produces an SVG element |
| `Helper_BarCode_EmptyValue_EmitsNothing` | Empty string produces no output |
| `Helper_BarCode_NullValue_EmitsNothing` | Null value produces no output |
| `Helper_BarCode_CustomColour_AppearsInSvg` | Custom bar-colour parameter reflected in SVG |
| `Helper_BarCode_DifferentFromQrCode` | SVG output differs from `qrCode` for the same value |

**Misc**

| Test | What's verified |
|---|---|
| `RenderAsync_WithPartial_ExpandsPartialContent` | `{{> partialName}}` expands registered partial |
| `RenderAsync_CancelledToken_ThrowsOperationCanceledException` | Pre-cancelled token causes `OperationCanceledException` |

---

## Integration Tests — `DocumentGenerator.IntegrationTests`

Integration tests wire **real** implementations together. `IDocumentRenderer` is always mocked (no Chromium), but `HandlebarsTemplateEngine`, `FileTemplateContentResolver`, and `DocumentPipeline` are the real classes exercised against live inputs.

### `Pipeline.RenderPipelineIntegrationTests` (10 tests)

**File:** `tests/DocumentGenerator.IntegrationTests/Pipeline/RenderPipelineIntegrationTests.cs`

Wires `HandlebarsTemplateEngine` + `DocumentPipeline` with a mock `IDocumentRenderer`. Tests the full template-to-renderer path end-to-end:

| Test | What's verified |
|---|---|
| `Pipeline_SimpleTemplate_ReturnsPdfBytes` | PDF bytes flow through the full pipeline |
| `Pipeline_TemplateWithVariables_RendererReceivesSubstitutedHtml` | Variable placeholders are gone by the time HTML reaches the renderer |
| `Pipeline_TemplateWithBranding_RendererReceivesBrandedHtml` | Branding substitutions applied before renderer |
| `Pipeline_TemplateWithCss_CssIsInjectedIntoHtml` | CSS injected as `<style>` block inside `<head>` |
| `Pipeline_UpperHelper_IsApplied` | `{{upper}}` helper functional in the integrated pipeline |
| `Pipeline_MultipleVariables_AllSubstituted` | Multiple distinct variables all substituted |
| `Pipeline_JobId_IsPreservedInResult` | `RenderResult.JobId` matches the request's `JobId` |
| `Pipeline_DocumentType_IsPreservedInResult` | `RenderResult.DocumentType` matches the template's `DocumentType` |
| `Pipeline_Partials_AreExpanded` | `{{> name}}` partials expanded end-to-end |
| `ServiceCollection_AddTemplating_RegistersITemplateEngine` | DI smoke test: `AddTemplating()` registers `ITemplateEngine` as `HandlebarsTemplateEngine` |

### `Worker.FileModeWorkerIntegrationTests` (8 tests)

**File:** `tests/DocumentGenerator.IntegrationTests/Worker/FileModeWorkerIntegrationTests.cs`

Uses `IDisposable` — creates unique temp `templates/` and `output/` directories and cleans them up after each test. Replicates the file-mode worker loop: read JSON → deserialise → render → write `.pdf`.

| Test | What's verified |
|---|---|
| `Template_SerialiseAndDeserialise_RoundTripsCorrectly` | `DocumentTemplate` round-trips through JSON serialisation |
| `Template_DeserialiseFromFile_ProducesCorrectTemplate` | Template deserialized from a file stream produces the correct model |
| `WorkerLoop_SingleTemplate_WritesPdfToOutputDirectory` | One JSON file → exactly one PDF in the output directory |
| `WorkerLoop_SingleTemplate_PdfBytesMatchRenderer` | Written PDF bytes match the renderer's return value exactly |
| `WorkerLoop_MultipleTemplates_WritesOnePdfPerTemplate` | Two JSON files → two PDFs |
| `WorkerLoop_OutputFilename_ContainsDocumentType` | Output filename starts with the template's `DocumentType` |
| `WorkerLoop_EmptyTemplateDirectory_ProducesNoPdfs` | No input files → no PDF files produced |
| `WorkerLoop_VariablesInTemplate_AreRenderedIntoHtml` | Variables substituted into HTML in the full file-loop path |

---

## Test Infrastructure Details

### IDisposable fixtures

Two test classes create real temp directories and clean them up via `IDisposable`:

- `FileTemplateContentResolverTests` — verifies path resolution against real files
- `FileModeWorkerIntegrationTests` — verifies the complete read→render→write loop

xUnit calls `Dispose()` after every test, so each test gets a fresh, isolated directory.

### No base classes

All test classes are `sealed` and self-contained. There is no shared base class or test fixture class. State shared within a class is set up in the constructor and is instance-scoped (reset per test).

### NullLogger

All constructors that accept real implementations pass `NullLogger<T>.Instance` from `Microsoft.Extensions.Logging.Abstractions`. This satisfies logger dependencies without producing any log output.

---

## Adding New Tests

### Unit test checklist

1. Create the class under the appropriate subfolder of `tests/DocumentGenerator.UnitTests/`.
2. Add a `[Fact]` or `[Theory]` per scenario — one assertion concern per test.
3. Use Moq for any interface dependencies; assert with FluentAssertions.
4. For helpers in `HandlebarsTemplateEngine`, follow the pattern in `HandlebarsTemplateEngineTests` — call `BuildTemplate(html: "…")` and call `engine.RenderAsync(template, CancellationToken.None)`.

### Integration test checklist

1. Create the class under `tests/DocumentGenerator.IntegrationTests/`.
2. Wire the real `HandlebarsTemplateEngine` and `DocumentPipeline` via `BuildPipeline()`.
3. Mock `IDocumentRenderer` — return fixed `byte[]` PDF content.
4. If you need temp files, implement `IDisposable` and clean up in `Dispose()`.

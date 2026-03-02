using DocumentGenerator.Bridge.Configuration;
using DocumentGenerator.Bridge.Printing;
using DocumentGenerator.Bridge.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DocumentGenerator.IntegrationTests.Bridge;

/// <summary>
/// <see cref="WebApplicationFactory{TEntryPoint}"/> for <c>DocumentGenerator.Bridge</c>.
/// Replaces external dependencies with stubs so no cloud API or local printer is needed:
/// <list type="bullet">
///   <item><see cref="IPrinterAdapter"/> → <see cref="MockPrinterAdapter"/></item>
///   <item><see cref="IHttpClientFactory"/> → <see cref="StubHttpClientFactory"/> backed by <see cref="MockCloudHandler"/></item>
/// </list>
/// </summary>
public sealed class BridgeWebApplicationFactory : WebApplicationFactory<DocumentGenerator.Bridge.Program>
{
    /// <summary>Fake Base64 document returned by the stub cloud handler.</summary>
    public const string FakeBase64 = "JVBERi0xLjQ=";

    /// <summary>Controllable stub printer adapter.</summary>
    public MockPrinterAdapter PrinterAdapter { get; } = new();

    /// <summary>Controllable stub cloud HTTP handler.</summary>
    public MockCloudHandler CloudHandler { get; } = new(FakeBase64);

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting($"{BridgeOptions.SectionName}:isConfigured",        "true");
        builder.UseSetting($"{BridgeOptions.SectionName}:port",                "0");
        builder.UseSetting($"{CloudOptions.SectionName}:baseUrl",              "http://fake-cloud");
        builder.UseSetting($"{CloudOptions.SectionName}:apiKey",               "test-key");
        builder.UseSetting($"{PrinterOptions.SectionName}:defaultPrinterName", "TestPrinter");
        builder.UseSetting($"{PrinterOptions.SectionName}:format",             "Pdf");

        builder.ConfigureServices(services =>
        {
            // Replace printer adapter
            services.RemoveAll<IPrinterAdapter>();
            services.AddSingleton<IPrinterAdapter>(PrinterAdapter);

            // Replace named HttpClient factory used by CloudBadgeClient
            services.RemoveAll<IHttpClientFactory>();
            services.AddSingleton<IHttpClientFactory>(
                new StubHttpClientFactory(CloudHandler));
        });
    }
}

// ── Stub printer adapter ──────────────────────────────────────────────────────

/// <summary>
/// In-memory <see cref="IPrinterAdapter"/> for tests. Records invocations.
/// </summary>
public sealed class MockPrinterAdapter : IPrinterAdapter
{
    /// <summary>Set to <c>true</c> after <see cref="PrintAsync"/> is invoked.</summary>
    public bool PrintCalled { get; set; }

    /// <summary>The printer name passed to the last <see cref="PrintAsync"/> call.</summary>
    public string? LastPrinterName { get; set; }

    /// <summary>Controls whether <see cref="PrintAsync"/> reports success (default: <c>true</c>).</summary>
    public bool ShouldSucceed { get; set; } = true;

    public IEnumerable<string> GetAvailablePrinters() => ["TestPrinter", "OfflinePrinter"];

    public Task<PrintResult> PrintAsync(
        byte[] documentBytes, string mimeType, string? printerName,
        string jobName, CancellationToken cancellationToken = default)
    {
        PrintCalled     = true;
        LastPrinterName = printerName;
        return Task.FromResult(ShouldSucceed
            ? PrintResult.Ok(printerName ?? "TestPrinter")
            : PrintResult.Fail("Printer offline", printerName));
    }
}

// ── Stub HTTP infrastructure for CloudBadgeClient ────────────────────────────

/// <summary>
/// HTTP message handler that returns canned cloud API JSON responses.
/// Routes on the request path so both the render and templates endpoints work correctly:
/// <list type="bullet">
///   <item><c>GET  .../templates</c> → JSON array of template names.</item>
///   <item><c>POST .../render</c>    → render response object.</item>
/// </list>
/// </summary>
public sealed class MockCloudHandler : HttpMessageHandler
{
    private readonly string _fakeBase64;

    /// <summary>Controls whether the handler returns a success response (default: <c>true</c>).</summary>
    public bool ShouldSucceed { get; set; } = true;

    public MockCloudHandler(string fakeBase64) => _fakeBase64 = fakeBase64;

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Route GET .../templates → return a plain JSON array of template names
        if (request.Method == System.Net.Http.HttpMethod.Get &&
            (request.RequestUri?.PathAndQuery.Contains("templates", StringComparison.OrdinalIgnoreCase) ?? false))
        {
            var templatesJson = "[\"badge-pulse-a6\",\"badge-executive-cc\"]";
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(templatesJson, System.Text.Encoding.UTF8, "application/json")
            });
        }

        // All other requests (POST .../render) — honour ShouldSucceed flag
        if (!ShouldSucceed)
        {
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("{\"success\":false,\"error\":\"cloud boom\"}")
            });
        }

        var json = $$"""
        {
            "correlationId": "{{Guid.NewGuid()}}",
            "jobId":         "{{Guid.NewGuid()}}",
            "success":       true,
            "documentBase64":"{{_fakeBase64}}",
            "mimeType":      "application/pdf",
            "documentType":  "badge",
            "elapsedTime":   "00:00:00.050",
            "completedAt":   "{{DateTimeOffset.UtcNow:O}}"
        }
        """;

        return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        });
    }
}

/// <summary>
/// Minimal <see cref="IHttpClientFactory"/> that always returns a client
/// backed by <see cref="MockCloudHandler"/>.
/// </summary>
public sealed class StubHttpClientFactory : IHttpClientFactory
{
    private readonly MockCloudHandler _handler;
    public StubHttpClientFactory(MockCloudHandler handler) => _handler = handler;

    public HttpClient CreateClient(string name) =>
        new(_handler) { BaseAddress = new Uri("http://fake-cloud") };
}

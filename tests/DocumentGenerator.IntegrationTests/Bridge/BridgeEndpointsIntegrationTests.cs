using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DocumentGenerator.Bridge.Models;
using Shouldly;
using Xunit;

namespace DocumentGenerator.IntegrationTests.Bridge;

/// <summary>
/// Integration tests for <c>DocumentGenerator.Bridge</c> endpoints using
/// <see cref="BridgeWebApplicationFactory"/> (in-process test server).
/// No real cloud API or local printer is needed — both are stubbed.
/// </summary>
public sealed class BridgeEndpointsIntegrationTests : IClassFixture<BridgeWebApplicationFactory>
{
    private readonly BridgeWebApplicationFactory _factory;
    private readonly HttpClient                  _client;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public BridgeEndpointsIntegrationTests(BridgeWebApplicationFactory factory)
    {
        _factory = factory;
        _client  = factory.CreateClient();
    }

    // ── GET /health ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Health_Get_Returns200()
    {
        var response = await _client.GetAsync("/health");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Health_Get_ReturnsHealthyStatus()
    {
        var response = await _client.GetAsync("/health");
        var body     = await response.Content.ReadAsStringAsync();
        body.ShouldContain("healthy");
    }

    [Fact]
    public async Task Health_Get_IsConfiguredIsTrue()
    {
        var response = await _client.GetAsync("/health");
        var json     = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("isConfigured").GetBoolean().ShouldBeTrue();
    }

    // ── GET /printers ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Printers_Get_Returns200()
    {
        var response = await _client.GetAsync("/printers");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Printers_Get_ReturnsAvailablePrinters()
    {
        var response  = await _client.GetAsync("/printers");
        var printers  = await response.Content.ReadFromJsonAsync<IEnumerable<string>>(JsonOpts);
        printers!.ShouldContain("TestPrinter");
    }

    // ── GET /templates ────────────────────────────────────────────────────────

    [Fact]
    public async Task Templates_Get_Returns200()
    {
        var response = await _client.GetAsync("/templates");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // ── POST /render ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Render_ValidRequest_Returns200()
    {
        var response = await _client.PostAsJsonAsync("/render", BuildPrintRequest());
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Render_ValidRequest_SuccessIsTrue()
    {
        var response = await _client.PostAsJsonAsync("/render", BuildPrintRequest());
        var body     = await response.Content.ReadFromJsonAsync<PrintResponse>(JsonOpts);
        body!.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task Render_ValidRequest_DocumentBase64IsPopulated()
    {
        var response = await _client.PostAsJsonAsync("/render", BuildPrintRequest());
        var body     = await response.Content.ReadFromJsonAsync<PrintResponse>(JsonOpts);
        body!.DocumentBase64.ShouldBe(BridgeWebApplicationFactory.FakeBase64);
    }

    [Fact]
    public async Task Render_ValidRequest_PrintedIsNull()
    {
        var response = await _client.PostAsJsonAsync("/render", BuildPrintRequest());
        var body     = await response.Content.ReadFromJsonAsync<PrintResponse>(JsonOpts);
        body!.Printed.ShouldBeNull();
    }

    [Fact]
    public async Task Render_ValidRequest_EchoesCorrelationId()
    {
        var correlationId = Guid.NewGuid();
        var response      = await _client.PostAsJsonAsync("/render", BuildPrintRequest(correlationId));
        var body          = await response.Content.ReadFromJsonAsync<PrintResponse>(JsonOpts);
        body!.CorrelationId.ShouldBe(correlationId);
    }

    [Fact]
    public async Task Render_DoesNotCallPrinter()
    {
        _factory.PrinterAdapter.PrintCalled = false; // reset
        await _client.PostAsJsonAsync("/render", BuildPrintRequest());
        _factory.PrinterAdapter.PrintCalled.ShouldBeFalse();
    }

    // ── POST /print ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Print_ValidRequest_Returns200()
    {
        var response = await _client.PostAsJsonAsync("/print", BuildPrintRequest());
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Print_ValidRequest_SuccessIsTrue()
    {
        var response = await _client.PostAsJsonAsync("/print", BuildPrintRequest());
        var body     = await response.Content.ReadFromJsonAsync<PrintResponse>(JsonOpts);
        body!.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task Print_ValidRequest_PrintedIsTrue()
    {
        var response = await _client.PostAsJsonAsync("/print", BuildPrintRequest());
        var body     = await response.Content.ReadFromJsonAsync<PrintResponse>(JsonOpts);
        (body!.Printed == true).ShouldBeTrue();
    }

    [Fact]
    public async Task Print_ValidRequest_DocumentBase64IsPresent()
    {
        var response = await _client.PostAsJsonAsync("/print", BuildPrintRequest());
        var body     = await response.Content.ReadFromJsonAsync<PrintResponse>(JsonOpts);
        body!.DocumentBase64.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task Print_ValidRequest_PrinterUsedIsSet()
    {
        var response = await _client.PostAsJsonAsync("/print", BuildPrintRequest());
        var body     = await response.Content.ReadFromJsonAsync<PrintResponse>(JsonOpts);
        body!.PrinterUsed.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task Print_ValidRequest_CallsPrinterAdapter()
    {
        _factory.PrinterAdapter.PrintCalled = false;  // reset
        await _client.PostAsJsonAsync("/print", BuildPrintRequest());
        _factory.PrinterAdapter.PrintCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task Print_SpecificPrinterName_ForwardsToAdapter()
    {
        var request  = BuildPrintRequest(printerName: "OfflinePrinter");
        await _client.PostAsJsonAsync("/print", request);
        _factory.PrinterAdapter.LastPrinterName.ShouldBe("OfflinePrinter");
    }

    [Fact]
    public async Task Print_PrinterFails_DocumentStillReturnedInResponse()
    {
        _factory.PrinterAdapter.ShouldSucceed = false;

        var response = await _client.PostAsJsonAsync("/print", BuildPrintRequest());
        var body     = await response.Content.ReadFromJsonAsync<PrintResponse>(JsonOpts);

        // Document is always returned, even if print fails
        body!.DocumentBase64.ShouldNotBeNullOrEmpty();
        (body.Printed == false).ShouldBeTrue();

        _factory.PrinterAdapter.ShouldSucceed = true; // restore
    }

    [Fact]
    public async Task Print_CloudFails_SuccessIsFalse()
    {
        _factory.CloudHandler.ShouldSucceed = false;

        var response = await _client.PostAsJsonAsync("/print", BuildPrintRequest());
        var body     = await response.Content.ReadFromJsonAsync<PrintResponse>(JsonOpts);

        body!.Success.ShouldBeFalse();

        _factory.CloudHandler.ShouldSucceed = true; // restore
    }

    // ── SetupGuardMiddleware ──────────────────────────────────────────────────

    [Fact]
    public async Task SetupGuard_WhenConfigured_AllowsNormalRequests()
    {
        // Bridge is configured in test factory — /render should work, not redirect
        var response = await _client.PostAsJsonAsync("/render", BuildPrintRequest());
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // ── GET /setup/printers ───────────────────────────────────────────────────

    [Fact]
    public async Task SetupPrinters_Get_Returns200()
    {
        var response = await _client.GetAsync("/setup/printers");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SetupPrinters_Get_ReturnsPrinterList()
    {
        var response = await _client.GetAsync("/setup/printers");
        var printers = await response.Content.ReadFromJsonAsync<IEnumerable<string>>(JsonOpts);
        printers.ShouldNotBeEmpty();
    }

    // ── POST /setup/test-connection ───────────────────────────────────────────

    [Fact]
    public async Task SetupTestConnection_ValidRequest_Returns200()
    {
        var response = await _client.PostAsJsonAsync("/setup/test-connection",
            new { baseUrl = "http://fake-cloud", apiKey = "test-key" });
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static object BuildPrintRequest(
        Guid?   correlationId = null,
        string? printerName   = null) =>
        new
        {
            templateName  = "badge-pulse-a6",
            variables     = new Dictionary<string, string>
            {
                ["firstName"] = "Jane",
                ["lastName"]  = "Smith",
                ["jobTitle"]  = "Engineer",
                ["company"]   = "Acme"
            },
            printerName,
            correlationId = correlationId ?? Guid.NewGuid()
        };
}

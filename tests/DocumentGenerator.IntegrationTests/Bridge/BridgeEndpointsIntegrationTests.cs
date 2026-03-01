using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DocumentGenerator.Bridge.Models;
using FluentAssertions;
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
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Health_Get_ReturnsHealthyStatus()
    {
        var response = await _client.GetAsync("/health");
        var body     = await response.Content.ReadAsStringAsync();
        body.Should().Contain("healthy");
    }

    [Fact]
    public async Task Health_Get_IsConfiguredIsTrue()
    {
        var response = await _client.GetAsync("/health");
        var json     = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("isConfigured").GetBoolean().Should().BeTrue();
    }

    // ── GET /printers ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Printers_Get_Returns200()
    {
        var response = await _client.GetAsync("/printers");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Printers_Get_ReturnsAvailablePrinters()
    {
        var response  = await _client.GetAsync("/printers");
        var printers  = await response.Content.ReadFromJsonAsync<IEnumerable<string>>(JsonOpts);
        printers.Should().Contain("TestPrinter");
    }

    // ── GET /templates ────────────────────────────────────────────────────────

    [Fact]
    public async Task Templates_Get_Returns200()
    {
        var response = await _client.GetAsync("/templates");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── POST /render ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Render_ValidRequest_Returns200()
    {
        var response = await _client.PostAsJsonAsync("/render", BuildPrintRequest());
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Render_ValidRequest_SuccessIsTrue()
    {
        var response = await _client.PostAsJsonAsync("/render", BuildPrintRequest());
        var body     = await response.Content.ReadFromJsonAsync<PrintResponse>(JsonOpts);
        body!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task Render_ValidRequest_DocumentBase64IsPopulated()
    {
        var response = await _client.PostAsJsonAsync("/render", BuildPrintRequest());
        var body     = await response.Content.ReadFromJsonAsync<PrintResponse>(JsonOpts);
        body!.DocumentBase64.Should().Be(BridgeWebApplicationFactory.FakeBase64);
    }

    [Fact]
    public async Task Render_ValidRequest_PrintedIsNull()
    {
        var response = await _client.PostAsJsonAsync("/render", BuildPrintRequest());
        var body     = await response.Content.ReadFromJsonAsync<PrintResponse>(JsonOpts);
        body!.Printed.Should().BeNull();
    }

    [Fact]
    public async Task Render_ValidRequest_EchoesCorrelationId()
    {
        var correlationId = Guid.NewGuid();
        var response      = await _client.PostAsJsonAsync("/render", BuildPrintRequest(correlationId));
        var body          = await response.Content.ReadFromJsonAsync<PrintResponse>(JsonOpts);
        body!.CorrelationId.Should().Be(correlationId);
    }

    [Fact]
    public async Task Render_DoesNotCallPrinter()
    {
        _factory.PrinterAdapter.PrintCalled = false; // reset
        await _client.PostAsJsonAsync("/render", BuildPrintRequest());
        _factory.PrinterAdapter.PrintCalled.Should().BeFalse();
    }

    // ── POST /print ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Print_ValidRequest_Returns200()
    {
        var response = await _client.PostAsJsonAsync("/print", BuildPrintRequest());
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Print_ValidRequest_SuccessIsTrue()
    {
        var response = await _client.PostAsJsonAsync("/print", BuildPrintRequest());
        var body     = await response.Content.ReadFromJsonAsync<PrintResponse>(JsonOpts);
        body!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task Print_ValidRequest_PrintedIsTrue()
    {
        var response = await _client.PostAsJsonAsync("/print", BuildPrintRequest());
        var body     = await response.Content.ReadFromJsonAsync<PrintResponse>(JsonOpts);
        body!.Printed.Should().BeTrue();
    }

    [Fact]
    public async Task Print_ValidRequest_DocumentBase64IsPresent()
    {
        var response = await _client.PostAsJsonAsync("/print", BuildPrintRequest());
        var body     = await response.Content.ReadFromJsonAsync<PrintResponse>(JsonOpts);
        body!.DocumentBase64.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Print_ValidRequest_PrinterUsedIsSet()
    {
        var response = await _client.PostAsJsonAsync("/print", BuildPrintRequest());
        var body     = await response.Content.ReadFromJsonAsync<PrintResponse>(JsonOpts);
        body!.PrinterUsed.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Print_ValidRequest_CallsPrinterAdapter()
    {
        _factory.PrinterAdapter.PrintCalled = false;  // reset
        await _client.PostAsJsonAsync("/print", BuildPrintRequest());
        _factory.PrinterAdapter.PrintCalled.Should().BeTrue();
    }

    [Fact]
    public async Task Print_SpecificPrinterName_ForwardsToAdapter()
    {
        var request  = BuildPrintRequest(printerName: "OfflinePrinter");
        await _client.PostAsJsonAsync("/print", request);
        _factory.PrinterAdapter.LastPrinterName.Should().Be("OfflinePrinter");
    }

    [Fact]
    public async Task Print_PrinterFails_DocumentStillReturnedInResponse()
    {
        _factory.PrinterAdapter.ShouldSucceed = false;

        var response = await _client.PostAsJsonAsync("/print", BuildPrintRequest());
        var body     = await response.Content.ReadFromJsonAsync<PrintResponse>(JsonOpts);

        // Document is always returned, even if print fails
        body!.DocumentBase64.Should().NotBeNullOrEmpty();
        body.Printed.Should().BeFalse();

        _factory.PrinterAdapter.ShouldSucceed = true; // restore
    }

    [Fact]
    public async Task Print_CloudFails_SuccessIsFalse()
    {
        _factory.CloudHandler.ShouldSucceed = false;

        var response = await _client.PostAsJsonAsync("/print", BuildPrintRequest());
        var body     = await response.Content.ReadFromJsonAsync<PrintResponse>(JsonOpts);

        body!.Success.Should().BeFalse();

        _factory.CloudHandler.ShouldSucceed = true; // restore
    }

    // ── SetupGuardMiddleware ──────────────────────────────────────────────────

    [Fact]
    public async Task SetupGuard_WhenConfigured_AllowsNormalRequests()
    {
        // Bridge is configured in test factory — /render should work, not redirect
        var response = await _client.PostAsJsonAsync("/render", BuildPrintRequest());
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── GET /setup/printers ───────────────────────────────────────────────────

    [Fact]
    public async Task SetupPrinters_Get_Returns200()
    {
        var response = await _client.GetAsync("/setup/printers");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SetupPrinters_Get_ReturnsPrinterList()
    {
        var response = await _client.GetAsync("/setup/printers");
        var printers = await response.Content.ReadFromJsonAsync<IEnumerable<string>>(JsonOpts);
        printers.Should().NotBeEmpty();
    }

    // ── POST /setup/test-connection ───────────────────────────────────────────

    [Fact]
    public async Task SetupTestConnection_ValidRequest_Returns200()
    {
        var response = await _client.PostAsJsonAsync("/setup/test-connection",
            new { baseUrl = "http://fake-cloud", apiKey = "test-key" });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
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

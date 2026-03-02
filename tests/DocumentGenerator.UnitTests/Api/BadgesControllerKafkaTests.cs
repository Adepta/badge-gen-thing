using DocumentGenerator.Api.Configuration;
using DocumentGenerator.Api.Controllers;
using DocumentGenerator.Api.Messaging;
using DocumentGenerator.Api.Models;
using DocumentGenerator.Api.Services;
using DocumentGenerator.Messaging.Messages;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Rebus.Bus;
using Shouldly;
using Xunit;

namespace DocumentGenerator.UnitTests.Api;

/// <summary>
/// Unit tests for <see cref="BadgesController"/> when the Kafka render path is active
/// (<c>Kafka:Enabled = true</c>).
///
/// <para>
/// The <see cref="IBus"/> is mocked to capture published messages without a real broker.
/// The <see cref="PendingRenderStore"/> is a real instance — results are injected directly
/// to simulate what the <see cref="DocumentRenderResultHandler"/> would do when a Kafka
/// result message arrives.
/// </para>
/// </summary>
public sealed class BadgesControllerKafkaTests : IDisposable
{
    private readonly Mock<IBus>               _busMock      = new();
    private readonly PendingRenderStore       _store        = new();
    private readonly string                   _tempDir;
    private readonly TemplateLocator          _locator;
    private readonly IOptions<ApiKafkaOptions> _kafkaOpts;
    private readonly BadgesController         _sut;

    private static readonly byte[] FakePdfBytes = [0x25, 0x50, 0x44, 0x46]; // %PDF

    public BadgesControllerKafkaTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"ctrl_kafka_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        File.WriteAllText(Path.Combine(_tempDir, "badge-pulse-a6.html"), "<p>{{variables.firstName}}</p>");
        File.WriteAllText(Path.Combine(_tempDir, "badge-pulse-a6.css"),  "body{}");

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DocumentGenerator:TemplatesPath"] = _tempDir
            })
            .Build();

        _locator = new TemplateLocator(config, NullLogger<TemplateLocator>.Instance);

        _kafkaOpts = Options.Create(new ApiKafkaOptions
        {
            Enabled              = true,
            BootstrapServers     = "localhost:9092",
            RequestTopic         = "render.requests",
            ResultTopic          = "render.results",
            ResultTimeoutSeconds = 5
        });

        // No real pipeline needed — Kafka path bypasses it
        var pipelineMock = new Mock<DocumentGenerator.Core.Interfaces.IDocumentPipeline>();

        _sut = new BadgesController(
            pipelineMock.Object,
            _locator,
            NullLogger<BadgesController>.Instance,
            _kafkaOpts,
            _busMock.Object,
            _store);
    }

    // ── Happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task RenderAsync_KafkaEnabled_PublishesMessageToBus()
    {
        ArrangeKafkaResult(success: true);
        await _sut.RenderAsync(BuildRequest(), CancellationToken.None);

        _busMock.Verify(b => b.Send(
            It.IsAny<DocumentRenderRequest>(),
            It.IsAny<IDictionary<string, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task RenderAsync_KafkaEnabled_Returns200OnSuccess()
    {
        ArrangeKafkaResult(success: true);
        var result = await _sut.RenderAsync(BuildRequest(), CancellationToken.None);

        var ok = result.ShouldBeOfType<OkObjectResult>();
        ok.StatusCode.ShouldBe(200);
    }

    [Fact]
    public async Task RenderAsync_KafkaEnabled_ResponseSuccessIsTrue()
    {
        ArrangeKafkaResult(success: true);
        var result = (OkObjectResult)await _sut.RenderAsync(BuildRequest(), CancellationToken.None);
        var body   = (BadgeRenderResponse)result.Value!;

        body.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task RenderAsync_KafkaEnabled_DocumentBase64IsPopulated()
    {
        ArrangeKafkaResult(success: true);
        var result = (OkObjectResult)await _sut.RenderAsync(BuildRequest(), CancellationToken.None);
        var body   = (BadgeRenderResponse)result.Value!;

        body.DocumentBase64.ShouldBe(Convert.ToBase64String(FakePdfBytes));
    }

    [Fact]
    public async Task RenderAsync_KafkaEnabled_EchoesCorrelationId()
    {
        var correlationId = Guid.NewGuid();
        ArrangeKafkaResult(success: true, correlationId: correlationId);

        var result = (OkObjectResult)await _sut.RenderAsync(
            BuildRequest(correlationId: correlationId), CancellationToken.None);
        var body = (BadgeRenderResponse)result.Value!;

        body.CorrelationId.ShouldBe(correlationId);
    }

    // ── Render failure from Console ───────────────────────────────────────────

    [Fact]
    public async Task RenderAsync_KafkaResultFailure_Returns500()
    {
        ArrangeKafkaResult(success: false);
        var result = await _sut.RenderAsync(BuildRequest(), CancellationToken.None);

        var obj = result.ShouldBeOfType<ObjectResult>();
        obj.StatusCode.ShouldBe(StatusCodes.Status500InternalServerError);
    }

    [Fact]
    public async Task RenderAsync_KafkaResultFailure_ResponseSuccessIsFalse()
    {
        ArrangeKafkaResult(success: false);
        var result = (ObjectResult)await _sut.RenderAsync(BuildRequest(), CancellationToken.None);
        var body   = (BadgeRenderResponse)result.Value!;

        body.Success.ShouldBeFalse();
        body.Error.ShouldNotBeNullOrEmpty();
    }

    // ── Timeout ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task RenderAsync_KafkaTimeout_Returns504()
    {
        // Bus.Send succeeds but no result is ever published — let it time out.
        // ResultTimeoutSeconds is 5 in test opts; use a very short value via new opts.
        var fastTimeoutOpts = Options.Create(new ApiKafkaOptions
        {
            Enabled              = true,
            ResultTimeoutSeconds = 1   // 1 second timeout for fast test
        });

        var pipelineMock = new Mock<DocumentGenerator.Core.Interfaces.IDocumentPipeline>();
        var sut = new BadgesController(
            pipelineMock.Object,
            _locator,
            NullLogger<BadgesController>.Instance,
            fastTimeoutOpts,
            _busMock.Object,
            _store);

        // Bus.Send does nothing — result never arrives
        _busMock.Setup(b => b.Send(It.IsAny<object>(), It.IsAny<IDictionary<string, string>>()))
            .Returns(Task.CompletedTask);

        var result = await sut.RenderAsync(BuildRequest(), CancellationToken.None);

        var obj = result.ShouldBeOfType<ObjectResult>();
        obj.StatusCode.ShouldBe(StatusCodes.Status504GatewayTimeout);
    }

    // ── Unknown template — same as inline path ────────────────────────────────

    [Fact]
    public async Task RenderAsync_UnknownTemplate_Returns400EvenWithKafkaEnabled()
    {
        var result = await _sut.RenderAsync(
            BuildRequest(templateName: "does-not-exist"), CancellationToken.None);

        var bad = result.ShouldBeOfType<BadRequestObjectResult>();
        bad.StatusCode.ShouldBe(400);

        // Bus should never be called if template resolution fails
        _busMock.Verify(b => b.Send(
            It.IsAny<object>(),
            It.IsAny<IDictionary<string, string>>()),
            Times.Never);
    }

    // ── Bus failure ───────────────────────────────────────────────────────────

    [Fact]
    public async Task RenderAsync_BusThrows_Returns500()
    {
        _busMock.Setup(b => b.Send(It.IsAny<object>(), It.IsAny<IDictionary<string, string>>()))
            .ThrowsAsync(new InvalidOperationException("Kafka unavailable"));

        var result = await _sut.RenderAsync(BuildRequest(), CancellationToken.None);

        var obj = result.ShouldBeOfType<ObjectResult>();
        obj.StatusCode.ShouldBe(StatusCodes.Status500InternalServerError);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Configures the mock bus to capture the published message's CorrelationId and
    /// immediately inject the matching result into the store (simulating the Console).
    /// </summary>
    private void ArrangeKafkaResult(bool success, Guid? correlationId = null)
    {
        _busMock
            .Setup(b => b.Send(It.IsAny<DocumentRenderRequest>(), It.IsAny<IDictionary<string, string>>()))
            .Callback<object, IDictionary<string, string>>((msg, _) =>
            {
                var req = (DocumentRenderRequest)msg;
                var id  = correlationId ?? req.CorrelationId;

                var result = success
                    ? DocumentRenderResult.Succeeded(id, "api", null, "badge",
                        FakePdfBytes, TimeSpan.FromMilliseconds(50))
                    : DocumentRenderResult.Failed(id, "api", null, "badge", "render failed");

                _store.TryComplete(result);
            })
            .Returns(Task.CompletedTask);
    }

    private static BadgeRenderRequest BuildRequest(
        string templateName  = "badge-pulse-a6",
        string format        = "Pdf",
        Guid?  correlationId = null) =>
        new()
        {
            TemplateName  = templateName,
            Variables     = new Dictionary<string, object?> { ["firstName"] = "Jane" },
            Format        = format,
            CorrelationId = correlationId
        };

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* best-effort */ }
    }
}

using DocumentGenerator.Api.Configuration;
using DocumentGenerator.Api.Messaging;
using DocumentGenerator.Core.Models;
using DocumentGenerator.Messaging.Messages;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Rebus.Activation;
using Rebus.Bus;
using Rebus.Config;
using Rebus.Kafka;
using Rebus.Routing.TypeBased;
using Testcontainers.Kafka;
using Xunit;
using MSOptions = Microsoft.Extensions.Options.Options;

namespace DocumentGenerator.IntegrationTests.Api;

/// <summary>
/// End-to-end integration tests for the Kafka render path.
///
/// <para>
/// A real Kafka broker is started in Docker via Testcontainers. Two independent
/// Rebus buses are wired in-process using <see cref="BuiltinHandlerActivator"/>
/// (no full DI host required):
/// </para>
/// <list type="bullet">
///   <item>
///     <term>API bus</term>
///     <description>
///       Publishes <see cref="DocumentRenderRequest"/> to <c>render.requests</c>
///       and consumes <see cref="DocumentRenderResult"/> from <c>render.results</c>
///       via a unique consumer group (mirroring the real API).
///       <see cref="PendingRenderStore"/> + <see cref="DocumentRenderResultHandler"/>
///       are wired so results complete the in-process awaiter.
///     </description>
///   </item>
///   <item>
///     <term>Console bus (stub)</term>
///     <description>
///       Consumes <see cref="DocumentRenderRequest"/> from <c>render.requests</c>
///       and immediately replies with a fake <see cref="DocumentRenderResult"/>,
///       standing in for the real Console render service.
///     </description>
///   </item>
/// </list>
///
/// <para>
/// Docker must be running for these tests to execute. The Kafka container is shared
/// across all tests in this class via <see cref="IAsyncLifetime"/>.
/// </para>
/// </summary>
[Trait("Category", "Kafka")]
public sealed class KafkaRenderIntegrationTests : IAsyncLifetime
{
    // ── Kafka topic names ────────────────────────────────────────────────────

    private const string RequestTopic = "render.requests";
    private const string ResultTopic  = "render.results";

    // ── Fake PDF bytes returned by the stub Console handler ──────────────────

    internal static readonly byte[] FakePdfBytes = [0x25, 0x50, 0x44, 0x46]; // %PDF

    // ── Infrastructure ───────────────────────────────────────────────────────

    private readonly KafkaContainer _kafkaContainer = new KafkaBuilder()
        .WithImage("confluentinc/cp-kafka:7.9.0")
        .Build();

    // Rebus activators (own the handler registrations and the bus lifetime)
    private BuiltinHandlerActivator? _apiActivator;
    private BuiltinHandlerActivator? _consoleActivator;

    // Public surface accessed by tests
    internal IBus          ApiBus    { get; private set; } = null!;
    internal PendingRenderStore Store { get; private set; } = null!;

    // ── IAsyncLifetime ───────────────────────────────────────────────────────

    /// <summary>
    /// Starts the Kafka container, then wires both the API-side and Console-side
    /// Rebus buses. A delay lets Rebus complete topic subscriptions before tests run.
    /// </summary>
    public async Task InitializeAsync()
    {
        await _kafkaContainer.StartAsync();
        var bootstrapServers = _kafkaContainer.GetBootstrapAddress();

        Store = new PendingRenderStore();

        // ── Console (stub render service) ────────────────────────────────────
        // Console bus — the handler receives the bus via the activator's Bus property
        // (captured after Start via a lazy reference)
        _consoleActivator = new BuiltinHandlerActivator();

        IBus? consoleBusRef = null;
        _consoleActivator.Register(() => new StubConsoleHandler(() => consoleBusRef!));

        Configure.With(_consoleActivator)
            .Transport(t => t.UseKafka(bootstrapServers, RequestTopic))
            .Routing(r => r.TypeBased().Map<DocumentRenderResult>(ResultTopic))
            .Logging(l => l.None())
            .Start();

        consoleBusRef = _consoleActivator.Bus;
        await _consoleActivator.Bus.Subscribe<DocumentRenderRequest>();

        // ── API (result receiver + publisher) ────────────────────────────────
        var resultHandler = new DocumentRenderResultHandler(
            Store,
            NullLogger<DocumentRenderResultHandler>.Instance);

        _apiActivator = new BuiltinHandlerActivator();
        _apiActivator.Register(() => resultHandler);

        var resultInputQueue = $"api-{Guid.NewGuid():N}";

        ApiBus = Configure.With(_apiActivator)
            .Transport(t => t.UseKafka(bootstrapServers, resultInputQueue))
            .Routing(r => r.TypeBased()
                .Map<DocumentRenderRequest>(RequestTopic)
                .Map<DocumentRenderResult>(ResultTopic))
            .Logging(l => l.None())
            .Start();

        await ApiBus.Subscribe<DocumentRenderResult>();

        // Allow Rebus worker threads to start and topic subscriptions to propagate
        await Task.Delay(TimeSpan.FromSeconds(5));
    }

    /// <summary>Disposes Rebus buses and the Kafka container.</summary>
    public async Task DisposeAsync()
    {
        _apiActivator?.Dispose();
        _consoleActivator?.Dispose();
        await _kafkaContainer.DisposeAsync();
    }

    // ── Tests ────────────────────────────────────────────────────────────────

    /// <summary>
    /// A <see cref="DocumentRenderRequest"/> published through the API bus is handled
    /// by the Console stub, which replies via Kafka. The <see cref="PendingRenderStore"/>
    /// awaiter must resolve within 15 seconds with a successful result.
    /// </summary>
    [Fact]
    public async Task RenderRequest_PublishedToKafka_ResultReturnsToApiStore()
    {
        var correlationId = Guid.NewGuid();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var resultTask = Store.RegisterAsync(correlationId, cts.Token);

        await ApiBus.Send(new DocumentRenderRequest
        {
            CorrelationId   = correlationId,
            DeviceId        = "test-device",
            Template        = MakeTemplate(),
            ReturnPdfInline = true
        });

        var result = await resultTask;

        result.CorrelationId.Should().Be(correlationId);
        result.Success.Should().BeTrue();
        result.PdfBase64.Should().Be(Convert.ToBase64String(FakePdfBytes));
    }

    /// <summary>
    /// Five concurrent requests must each resolve to their own correlation ID —
    /// the store must not mix up results even when they arrive in a different order.
    /// </summary>
    [Fact]
    public async Task MultipleRequests_AllResolveToCorrectCorrelationId()
    {
        const int count = 5;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var correlationIds = Enumerable.Range(0, count).Select(_ => Guid.NewGuid()).ToList();

        // Register all awaiters before publishing so none are missed
        var resultTasks = correlationIds
            .Select(id => (Id: id, Task: Store.RegisterAsync(id, cts.Token)))
            .ToList();

        foreach (var id in correlationIds)
        {
            await ApiBus.Send(new DocumentRenderRequest
            {
                CorrelationId   = id,
                DeviceId        = "test-device",
                Template        = MakeTemplate(),
                ReturnPdfInline = true
            });
        }

        await Task.WhenAll(resultTasks.Select(t => t.Task));

        foreach (var (id, task) in resultTasks)
        {
            var result = await task;
            result.CorrelationId.Should().Be(id);
            result.Success.Should().BeTrue();
        }
    }

    /// <summary>
    /// When the stub Console handler signals a failure, the awaiter receives
    /// <see cref="DocumentRenderResult.Success"/> = <see langword="false"/> with a
    /// non-empty <see cref="DocumentRenderResult.ErrorMessage"/>.
    /// </summary>
    [Fact]
    public async Task FailureResult_PropagatesErrorToAwaiter()
    {
        var correlationId = Guid.NewGuid();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var resultTask = Store.RegisterAsync(correlationId, cts.Token);

        // Special device ID instructs the stub to return a failure
        await ApiBus.Send(new DocumentRenderRequest
        {
            CorrelationId   = correlationId,
            DeviceId        = StubConsoleHandler.FailDeviceId,
            Template        = MakeTemplate(),
            ReturnPdfInline = true
        });

        var result = await resultTask;

        result.CorrelationId.Should().Be(correlationId);
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    /// <summary>
    /// When the awaiter's <see cref="CancellationToken"/> fires (simulating the API
    /// timeout) the task must be cancelled and the store must contain zero pending entries.
    /// </summary>
    [Fact]
    public async Task Timeout_CancelsAwaiterAndRemovesFromStore()
    {
        var correlationId = Guid.NewGuid();

        // Very short timeout — do NOT publish a request so no result arrives
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        var resultTask = Store.RegisterAsync(correlationId, cts.Token);

        Func<Task> act = () => resultTask;
        await act.Should().ThrowAsync<OperationCanceledException>();

        Store.PendingCount.Should().Be(0,
            "cancelled awaiter must be removed from the store");
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private static DocumentTemplate MakeTemplate() => new()
    {
        DocumentType = "badge",
        Template     = new TemplateContent
        {
            Html = "<p>{{variables.firstName}}</p>",
            Css  = "body{}"
        }
    };
}

// ── Stub Console handler ─────────────────────────────────────────────────────

/// <summary>
/// Stands in for the real <c>DocumentGenerator.Console</c> render worker during tests.
/// Immediately replies with a pre-built <see cref="DocumentRenderResult"/> — no Chromium needed.
/// Using <see cref="FailDeviceId"/> as the request device ID causes a failure reply instead.
/// </summary>
/// <param name="getBus">
/// Factory that returns the Rebus bus to reply on. Provided as a lazy getter so the
/// handler can be registered before the bus reference is available from
/// <see cref="BuiltinHandlerActivator.Bus"/> (which is populated only after
/// <see cref="Configure"/> calls <c>Start()</c>).
/// </param>
file sealed class StubConsoleHandler(Func<IBus> getBus)
    : Rebus.Handlers.IHandleMessages<DocumentRenderRequest>
{
    /// <summary>Device ID that triggers a failure result from the stub.</summary>
    public const string FailDeviceId = "fail-device";

    /// <inheritdoc/>
    public async Task Handle(DocumentRenderRequest message)
    {
        DocumentRenderResult result = message.DeviceId == FailDeviceId
            ? DocumentRenderResult.Failed(
                message.CorrelationId, message.DeviceId, message.SessionId,
                message.Template.DocumentType, "Stub render failure")
            : DocumentRenderResult.Succeeded(
                message.CorrelationId, message.DeviceId, message.SessionId,
                message.Template.DocumentType,
                KafkaRenderIntegrationTests.FakePdfBytes,
                TimeSpan.FromMilliseconds(10),
                returnInline: true);

        await getBus().Reply(result);
    }
}

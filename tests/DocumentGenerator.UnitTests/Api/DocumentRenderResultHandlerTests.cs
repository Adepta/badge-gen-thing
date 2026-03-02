using DocumentGenerator.Api.Messaging;
using DocumentGenerator.Messaging.Messages;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace DocumentGenerator.UnitTests.Api;

/// <summary>
/// Unit tests for <see cref="DocumentRenderResultHandler"/>.
/// </summary>
public sealed class DocumentRenderResultHandlerTests
{
    private static DocumentRenderResult MakeResult(Guid correlationId, bool success = true) =>
        success
            ? DocumentRenderResult.Succeeded(correlationId, "device-1", null, "badge",
                [0x25, 0x50, 0x44, 0x46], TimeSpan.FromMilliseconds(50))
            : DocumentRenderResult.Failed(correlationId, "device-1", null, "badge", "render failed");

    // ── Handle — known correlation ID ─────────────────────────────────────────

    [Fact]
    public async Task Handle_KnownCorrelationId_ResolvesAwaiter()
    {
        var store         = new PendingRenderStore();
        var correlationId = Guid.NewGuid();
        var resultTask    = store.RegisterAsync(correlationId, CancellationToken.None);

        var handler = new DocumentRenderResultHandler(store,
            NullLogger<DocumentRenderResultHandler>.Instance);

        await handler.Handle(MakeResult(correlationId));

        resultTask.IsCompletedSuccessfully.ShouldBeTrue();
    }

    [Fact]
    public async Task Handle_KnownCorrelationId_ReturnsCorrectResult()
    {
        var store         = new PendingRenderStore();
        var correlationId = Guid.NewGuid();
        var resultTask    = store.RegisterAsync(correlationId, CancellationToken.None);
        var expected      = MakeResult(correlationId);

        var handler = new DocumentRenderResultHandler(store,
            NullLogger<DocumentRenderResultHandler>.Instance);

        await handler.Handle(expected);

        var resolved = await resultTask;
        resolved.CorrelationId.ShouldBe(correlationId);
        resolved.Success.ShouldBeTrue();
    }

    // ── Handle — unknown correlation ID (different instance) ──────────────────

    [Fact]
    public async Task Handle_UnknownCorrelationId_DoesNotThrow()
    {
        var store   = new PendingRenderStore();
        var handler = new DocumentRenderResultHandler(store,
            NullLogger<DocumentRenderResultHandler>.Instance);

        // No awaiter registered — should silently discard
        var act = async () => await handler.Handle(MakeResult(Guid.NewGuid()));
        await Should.NotThrowAsync(act);
    }

    [Fact]
    public async Task Handle_UnknownCorrelationId_DoesNotAffectOtherAwaiters()
    {
        var store          = new PendingRenderStore();
        var knownId        = Guid.NewGuid();
        var unknownId      = Guid.NewGuid();
        var resultTask     = store.RegisterAsync(knownId, CancellationToken.None);

        var handler = new DocumentRenderResultHandler(store,
            NullLogger<DocumentRenderResultHandler>.Instance);

        // Handle result for a completely different correlation ID
        await handler.Handle(MakeResult(unknownId));

        // The known awaiter should still be pending
        resultTask.IsCompleted.ShouldBeFalse();
        store.PendingCount.ShouldBe(1);
    }

    // ── Handle — failure result ───────────────────────────────────────────────

    [Fact]
    public async Task Handle_FailureResult_ResolvesAwaiterWithFailure()
    {
        var store         = new PendingRenderStore();
        var correlationId = Guid.NewGuid();
        var resultTask    = store.RegisterAsync(correlationId, CancellationToken.None);
        var failureResult = MakeResult(correlationId, success: false);

        var handler = new DocumentRenderResultHandler(store,
            NullLogger<DocumentRenderResultHandler>.Instance);

        await handler.Handle(failureResult);

        var resolved = await resultTask;
        resolved.Success.ShouldBeFalse();
        resolved.ErrorMessage.ShouldBe("render failed");
    }
}

using DocumentGenerator.Api.Messaging;
using DocumentGenerator.Messaging.Messages;
using Shouldly;
using Xunit;

namespace DocumentGenerator.UnitTests.Api;

/// <summary>
/// Unit tests for <see cref="PendingRenderStore"/>.
/// </summary>
public sealed class PendingRenderStoreTests
{
    private static DocumentRenderResult MakeResult(Guid correlationId, bool success = true) =>
        success
            ? DocumentRenderResult.Succeeded(correlationId, "device-1", null, "badge",
                [0x25, 0x50, 0x44, 0x46], TimeSpan.FromMilliseconds(50))
            : DocumentRenderResult.Failed(correlationId, "device-1", null, "badge", "boom");

    // ── RegisterAsync / TryComplete ───────────────────────────────────────────

    [Fact]
    public async Task TryComplete_MatchingCorrelationId_ResolvesTask()
    {
        var store         = new PendingRenderStore();
        var correlationId = Guid.NewGuid();
        var resultTask    = store.RegisterAsync(correlationId, CancellationToken.None);
        var result        = MakeResult(correlationId);

        var resolved = store.TryComplete(result);
        var awaited  = await resultTask;

        resolved.ShouldBeTrue();
        awaited.CorrelationId.ShouldBe(correlationId);
    }

    [Fact]
    public async Task TryComplete_ResolvesWithCorrectResult()
    {
        var store         = new PendingRenderStore();
        var correlationId = Guid.NewGuid();
        var resultTask    = store.RegisterAsync(correlationId, CancellationToken.None);
        var result        = MakeResult(correlationId);

        store.TryComplete(result);
        var awaited = await resultTask;

        awaited.Success.ShouldBeTrue();
        awaited.PdfBase64.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void TryComplete_UnknownCorrelationId_ReturnsFalse()
    {
        var store  = new PendingRenderStore();
        var result = MakeResult(Guid.NewGuid());

        var resolved = store.TryComplete(result);

        resolved.ShouldBeFalse();
    }

    [Fact]
    public void TryComplete_AfterCancellation_ReturnsFalse()
    {
        var store         = new PendingRenderStore();
        var correlationId = Guid.NewGuid();
        using var cts     = new CancellationTokenSource();

        _ = store.RegisterAsync(correlationId, cts.Token);
        cts.Cancel();

        // After cancellation the awaiter is removed — TryComplete should not find it
        var result   = MakeResult(correlationId);
        var resolved = store.TryComplete(result);

        resolved.ShouldBeFalse();
    }

    [Fact]
    public async Task RegisterAsync_WhenCancelled_TaskIsCancelled()
    {
        var store         = new PendingRenderStore();
        var correlationId = Guid.NewGuid();
        using var cts     = new CancellationTokenSource();

        var resultTask = store.RegisterAsync(correlationId, cts.Token);
        cts.Cancel();

        await Should.ThrowAsync<OperationCanceledException>(async () => await resultTask);
    }

    // ── PendingCount ──────────────────────────────────────────────────────────

    [Fact]
    public void PendingCount_StartsAtZero()
    {
        var store = new PendingRenderStore();
        store.PendingCount.ShouldBe(0);
    }

    [Fact]
    public void PendingCount_IncreasesOnRegister()
    {
        var store = new PendingRenderStore();
        _ = store.RegisterAsync(Guid.NewGuid(), CancellationToken.None);
        _ = store.RegisterAsync(Guid.NewGuid(), CancellationToken.None);

        store.PendingCount.ShouldBe(2);
    }

    [Fact]
    public void PendingCount_DecreasesOnTryComplete()
    {
        var store         = new PendingRenderStore();
        var correlationId = Guid.NewGuid();
        _ = store.RegisterAsync(correlationId, CancellationToken.None);

        store.TryComplete(MakeResult(correlationId));

        store.PendingCount.ShouldBe(0);
    }

    // ── CancelAll ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task CancelAll_CancelsAllPendingTasks()
    {
        var store = new PendingRenderStore();
        var t1    = store.RegisterAsync(Guid.NewGuid(), CancellationToken.None);
        var t2    = store.RegisterAsync(Guid.NewGuid(), CancellationToken.None);

        store.CancelAll();

        await Should.ThrowAsync<OperationCanceledException>(async () => await t1);
        await Should.ThrowAsync<OperationCanceledException>(async () => await t2);
    }

    [Fact]
    public void CancelAll_ResetsPendingCountToZero()
    {
        var store = new PendingRenderStore();
        _ = store.RegisterAsync(Guid.NewGuid(), CancellationToken.None);
        _ = store.RegisterAsync(Guid.NewGuid(), CancellationToken.None);

        store.CancelAll();

        store.PendingCount.ShouldBe(0);
    }

    [Fact]
    public void CancelAll_OnEmptyStore_DoesNotThrow()
    {
        var store = new PendingRenderStore();
        Should.NotThrow(() => store.CancelAll());
    }
}

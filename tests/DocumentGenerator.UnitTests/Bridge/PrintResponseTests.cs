using DocumentGenerator.Bridge.Models;
using FluentAssertions;
using Xunit;

namespace DocumentGenerator.UnitTests.Bridge;

/// <summary>
/// Unit tests for <see cref="PrintResponse"/> factory methods.
/// All tests are purely in-memory — no I/O, no HTTP.
/// </summary>
public sealed class PrintResponseTests
{
    private const string FakeBase64 = "JVBERi0x"; // partial PDF base64

    // ── RenderOk ──────────────────────────────────────────────────────────────

    [Fact]
    public void RenderOk_SetsSuccessTrue()
    {
        var r = PrintResponse.RenderOk(Guid.NewGuid(), FakeBase64, "application/pdf", TimeSpan.Zero);
        r.Success.Should().BeTrue();
    }

    [Fact]
    public void RenderOk_SetsDocumentBase64()
    {
        var r = PrintResponse.RenderOk(Guid.NewGuid(), FakeBase64, "application/pdf", TimeSpan.Zero);
        r.DocumentBase64.Should().Be(FakeBase64);
    }

    [Fact]
    public void RenderOk_SetsMimeType()
    {
        var r = PrintResponse.RenderOk(Guid.NewGuid(), FakeBase64, "image/png", TimeSpan.Zero);
        r.MimeType.Should().Be("image/png");
    }

    [Fact]
    public void RenderOk_PrintedIsNull()
    {
        var r = PrintResponse.RenderOk(Guid.NewGuid(), FakeBase64, "application/pdf", TimeSpan.Zero);
        r.Printed.Should().BeNull();
    }

    [Fact]
    public void RenderOk_PrinterUsedIsNull()
    {
        var r = PrintResponse.RenderOk(Guid.NewGuid(), FakeBase64, "application/pdf", TimeSpan.Zero);
        r.PrinterUsed.Should().BeNull();
    }

    [Fact]
    public void RenderOk_EchoesCorrelationId()
    {
        var id = Guid.NewGuid();
        var r  = PrintResponse.RenderOk(id, FakeBase64, "application/pdf", TimeSpan.Zero);
        r.CorrelationId.Should().Be(id);
    }

    [Fact]
    public void RenderOk_SetsElapsedTime()
    {
        var elapsed = TimeSpan.FromMilliseconds(512);
        var r       = PrintResponse.RenderOk(Guid.NewGuid(), FakeBase64, "application/pdf", elapsed);
        r.ElapsedTime.Should().Be(elapsed);
    }

    [Fact]
    public void RenderOk_CompletedAtIsApproximatelyNow()
    {
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);
        var r      = PrintResponse.RenderOk(Guid.NewGuid(), FakeBase64, "application/pdf", TimeSpan.Zero);
        var after  = DateTimeOffset.UtcNow.AddSeconds(1);
        r.CompletedAt.Should().BeAfter(before).And.BeBefore(after);
    }

    // ── PrintOk ───────────────────────────────────────────────────────────────

    [Fact]
    public void PrintOk_SetsSuccessTrue()
    {
        var r = PrintResponse.PrintOk(Guid.NewGuid(), FakeBase64, "application/pdf", "HP LaserJet", TimeSpan.Zero);
        r.Success.Should().BeTrue();
    }

    [Fact]
    public void PrintOk_SetsPrintedTrue()
    {
        var r = PrintResponse.PrintOk(Guid.NewGuid(), FakeBase64, "application/pdf", "HP LaserJet", TimeSpan.Zero);
        r.Printed.Should().BeTrue();
    }

    [Fact]
    public void PrintOk_SetsPrinterUsed()
    {
        var r = PrintResponse.PrintOk(Guid.NewGuid(), FakeBase64, "application/pdf", "HP LaserJet", TimeSpan.Zero);
        r.PrinterUsed.Should().Be("HP LaserJet");
    }

    [Fact]
    public void PrintOk_ErrorIsNull()
    {
        var r = PrintResponse.PrintOk(Guid.NewGuid(), FakeBase64, "application/pdf", "HP LaserJet", TimeSpan.Zero);
        r.Error.Should().BeNull();
    }

    // ── Fail ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Fail_SetsSuccessFalse()
    {
        var r = PrintResponse.Fail(Guid.NewGuid(), "cloud error", TimeSpan.Zero);
        r.Success.Should().BeFalse();
    }

    [Fact]
    public void Fail_SetsError()
    {
        var r = PrintResponse.Fail(Guid.NewGuid(), "cloud error", TimeSpan.Zero);
        r.Error.Should().Be("cloud error");
    }

    [Fact]
    public void Fail_DocumentBase64IsNull()
    {
        var r = PrintResponse.Fail(Guid.NewGuid(), "err", TimeSpan.Zero);
        r.DocumentBase64.Should().BeNull();
    }

    [Fact]
    public void Fail_EchoesCorrelationId()
    {
        var id = Guid.NewGuid();
        var r  = PrintResponse.Fail(id, "err", TimeSpan.Zero);
        r.CorrelationId.Should().Be(id);
    }

    [Fact]
    public void Fail_WithErrorCode_SetsErrorCode()
    {
        var r = PrintResponse.Fail(Guid.NewGuid(), "cloud render failed", TimeSpan.Zero, "DG5001");
        r.ErrorCode.Should().Be("DG5001");
    }

    [Fact]
    public void Fail_WithoutErrorCode_ErrorCodeIsNull()
    {
        var r = PrintResponse.Fail(Guid.NewGuid(), "err", TimeSpan.Zero);
        r.ErrorCode.Should().BeNull();
    }

    [Fact]
    public void RenderOk_ErrorCodeIsNull()
    {
        var r = PrintResponse.RenderOk(Guid.NewGuid(), FakeBase64, "application/pdf", TimeSpan.Zero);
        r.ErrorCode.Should().BeNull();
    }

    [Fact]
    public void PrintOk_ErrorCodeIsNull()
    {
        var r = PrintResponse.PrintOk(Guid.NewGuid(), FakeBase64, "application/pdf", "HP LaserJet", TimeSpan.Zero);
        r.ErrorCode.Should().BeNull();
    }
}

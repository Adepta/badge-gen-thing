using DocumentGenerator.Bridge.Models;
using Shouldly;
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
        r.Success.ShouldBeTrue();
    }

    [Fact]
    public void RenderOk_SetsDocumentBase64()
    {
        var r = PrintResponse.RenderOk(Guid.NewGuid(), FakeBase64, "application/pdf", TimeSpan.Zero);
        r.DocumentBase64.ShouldBe(FakeBase64);
    }

    [Fact]
    public void RenderOk_SetsMimeType()
    {
        var r = PrintResponse.RenderOk(Guid.NewGuid(), FakeBase64, "image/png", TimeSpan.Zero);
        r.MimeType.ShouldBe("image/png");
    }

    [Fact]
    public void RenderOk_PrintedIsNull()
    {
        var r = PrintResponse.RenderOk(Guid.NewGuid(), FakeBase64, "application/pdf", TimeSpan.Zero);
        r.Printed.ShouldBeNull();
    }

    [Fact]
    public void RenderOk_PrinterUsedIsNull()
    {
        var r = PrintResponse.RenderOk(Guid.NewGuid(), FakeBase64, "application/pdf", TimeSpan.Zero);
        r.PrinterUsed.ShouldBeNull();
    }

    [Fact]
    public void RenderOk_EchoesCorrelationId()
    {
        var id = Guid.NewGuid();
        var r  = PrintResponse.RenderOk(id, FakeBase64, "application/pdf", TimeSpan.Zero);
        r.CorrelationId.ShouldBe(id);
    }

    [Fact]
    public void RenderOk_SetsElapsedTime()
    {
        var elapsed = TimeSpan.FromMilliseconds(512);
        var r       = PrintResponse.RenderOk(Guid.NewGuid(), FakeBase64, "application/pdf", elapsed);
        r.ElapsedTime.ShouldBe(elapsed);
    }

    [Fact]
    public void RenderOk_CompletedAtIsApproximatelyNow()
    {
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);
        var r      = PrintResponse.RenderOk(Guid.NewGuid(), FakeBase64, "application/pdf", TimeSpan.Zero);
        var after  = DateTimeOffset.UtcNow.AddSeconds(1);
        r.CompletedAt.ShouldBeGreaterThan(before);
        r.CompletedAt.ShouldBeLessThan(after);
    }

    // ── PrintOk ───────────────────────────────────────────────────────────────

    [Fact]
    public void PrintOk_SetsSuccessTrue()
    {
        var r = PrintResponse.PrintOk(Guid.NewGuid(), FakeBase64, "application/pdf", "HP LaserJet", TimeSpan.Zero);
        r.Success.ShouldBeTrue();
    }

    [Fact]
    public void PrintOk_SetsPrintedTrue()
    {
        var r = PrintResponse.PrintOk(Guid.NewGuid(), FakeBase64, "application/pdf", "HP LaserJet", TimeSpan.Zero);
        (r.Printed == true).ShouldBeTrue();
    }

    [Fact]
    public void PrintOk_SetsPrinterUsed()
    {
        var r = PrintResponse.PrintOk(Guid.NewGuid(), FakeBase64, "application/pdf", "HP LaserJet", TimeSpan.Zero);
        r.PrinterUsed.ShouldBe("HP LaserJet");
    }

    [Fact]
    public void PrintOk_ErrorIsNull()
    {
        var r = PrintResponse.PrintOk(Guid.NewGuid(), FakeBase64, "application/pdf", "HP LaserJet", TimeSpan.Zero);
        r.Error.ShouldBeNull();
    }

    // ── Fail ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Fail_SetsSuccessFalse()
    {
        var r = PrintResponse.Fail(Guid.NewGuid(), "cloud error", TimeSpan.Zero);
        r.Success.ShouldBeFalse();
    }

    [Fact]
    public void Fail_SetsError()
    {
        var r = PrintResponse.Fail(Guid.NewGuid(), "cloud error", TimeSpan.Zero);
        r.Error.ShouldBe("cloud error");
    }

    [Fact]
    public void Fail_DocumentBase64IsNull()
    {
        var r = PrintResponse.Fail(Guid.NewGuid(), "err", TimeSpan.Zero);
        r.DocumentBase64.ShouldBeNull();
    }

    [Fact]
    public void Fail_EchoesCorrelationId()
    {
        var id = Guid.NewGuid();
        var r  = PrintResponse.Fail(id, "err", TimeSpan.Zero);
        r.CorrelationId.ShouldBe(id);
    }

    [Fact]
    public void Fail_WithErrorCode_SetsErrorCode()
    {
        var r = PrintResponse.Fail(Guid.NewGuid(), "cloud render failed", TimeSpan.Zero, "DG5001");
        r.ErrorCode.ShouldBe("DG5001");
    }

    [Fact]
    public void Fail_WithoutErrorCode_ErrorCodeIsNull()
    {
        var r = PrintResponse.Fail(Guid.NewGuid(), "err", TimeSpan.Zero);
        r.ErrorCode.ShouldBeNull();
    }

    [Fact]
    public void RenderOk_ErrorCodeIsNull()
    {
        var r = PrintResponse.RenderOk(Guid.NewGuid(), FakeBase64, "application/pdf", TimeSpan.Zero);
        r.ErrorCode.ShouldBeNull();
    }

    [Fact]
    public void PrintOk_ErrorCodeIsNull()
    {
        var r = PrintResponse.PrintOk(Guid.NewGuid(), FakeBase64, "application/pdf", "HP LaserJet", TimeSpan.Zero);
        r.ErrorCode.ShouldBeNull();
    }
}

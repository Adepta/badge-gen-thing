using DocumentGenerator.Bridge.Printing;
using Shouldly;
using Xunit;

namespace DocumentGenerator.UnitTests.Bridge;

/// <summary>
/// Unit tests for <see cref="PrintResult"/> factory methods.
/// </summary>
public sealed class PrintResultTests
{
    // ── Ok ────────────────────────────────────────────────────────────────────

    [Fact]
    public void Ok_SetsSuccessTrue()
    {
        var r = PrintResult.Ok("HP LaserJet");
        r.Success.ShouldBeTrue();
    }

    [Fact]
    public void Ok_SetsPrinterUsed()
    {
        var r = PrintResult.Ok("Zebra ZD621");
        r.PrinterUsed.ShouldBe("Zebra ZD621");
    }

    [Fact]
    public void Ok_ErrorIsNull()
    {
        var r = PrintResult.Ok("HP LaserJet");
        r.Error.ShouldBeNull();
    }

    // ── Fail ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Fail_SetsSuccessFalse()
    {
        var r = PrintResult.Fail("spooler error");
        r.Success.ShouldBeFalse();
    }

    [Fact]
    public void Fail_SetsError()
    {
        var r = PrintResult.Fail("printer offline");
        r.Error.ShouldBe("printer offline");
    }

    [Fact]
    public void Fail_PrinterUsedIsNullWhenNotProvided()
    {
        var r = PrintResult.Fail("err");
        r.PrinterUsed.ShouldBeNull();
    }

    [Fact]
    public void Fail_SetsPrinterUsedWhenProvided()
    {
        var r = PrintResult.Fail("err", "Brother QL-820NWB");
        r.PrinterUsed.ShouldBe("Brother QL-820NWB");
    }
}

using DocumentGenerator.Bridge.Printing;
using FluentAssertions;
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
        r.Success.Should().BeTrue();
    }

    [Fact]
    public void Ok_SetsPrinterUsed()
    {
        var r = PrintResult.Ok("Zebra ZD621");
        r.PrinterUsed.Should().Be("Zebra ZD621");
    }

    [Fact]
    public void Ok_ErrorIsNull()
    {
        var r = PrintResult.Ok("HP LaserJet");
        r.Error.Should().BeNull();
    }

    // ── Fail ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Fail_SetsSuccessFalse()
    {
        var r = PrintResult.Fail("spooler error");
        r.Success.Should().BeFalse();
    }

    [Fact]
    public void Fail_SetsError()
    {
        var r = PrintResult.Fail("printer offline");
        r.Error.Should().Be("printer offline");
    }

    [Fact]
    public void Fail_PrinterUsedIsNullWhenNotProvided()
    {
        var r = PrintResult.Fail("err");
        r.PrinterUsed.Should().BeNull();
    }

    [Fact]
    public void Fail_SetsPrinterUsedWhenProvided()
    {
        var r = PrintResult.Fail("err", "Brother QL-820NWB");
        r.PrinterUsed.Should().Be("Brother QL-820NWB");
    }
}

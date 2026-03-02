using DocumentGenerator.Bridge.Configuration;
using Shouldly;
using Xunit;

namespace DocumentGenerator.UnitTests.Bridge;

/// <summary>
/// Unit tests for <see cref="BridgeOptions"/>, <see cref="CloudOptions"/>,
/// and <see cref="PrinterOptions"/> default values and section name constants.
/// </summary>
public sealed class BridgeConfigurationTests
{
    // ── BridgeOptions ─────────────────────────────────────────────────────────

    [Fact]
    public void BridgeOptions_SectionName_IsBridge()
    {
        BridgeOptions.SectionName.ShouldBe("Bridge");
    }

    [Fact]
    public void BridgeOptions_DefaultPort_Is5100()
    {
        new BridgeOptions().Port.ShouldBe(5100);
    }

    [Fact]
    public void BridgeOptions_DefaultIsConfigured_IsFalse()
    {
        new BridgeOptions().IsConfigured.ShouldBeFalse();
    }

    // ── CloudOptions ──────────────────────────────────────────────────────────

    [Fact]
    public void CloudOptions_SectionName_IsCloud()
    {
        CloudOptions.SectionName.ShouldBe("Cloud");
    }

    [Fact]
    public void CloudOptions_DefaultBaseUrl_IsEmpty()
    {
        new CloudOptions().BaseUrl.ShouldBeEmpty();
    }

    [Fact]
    public void CloudOptions_DefaultApiKey_IsEmpty()
    {
        new CloudOptions().ApiKey.ShouldBeEmpty();
    }

    [Fact]
    public void CloudOptions_DefaultTimeout_Is30Seconds()
    {
        new CloudOptions().Timeout.ShouldBe(TimeSpan.FromSeconds(30));
    }

    // ── PrinterOptions ────────────────────────────────────────────────────────

    [Fact]
    public void PrinterOptions_SectionName_IsPrinter()
    {
        PrinterOptions.SectionName.ShouldBe("Printer");
    }

    [Fact]
    public void PrinterOptions_DefaultPrinterName_IsNull()
    {
        new PrinterOptions().DefaultPrinterName.ShouldBeNull();
    }

    [Fact]
    public void PrinterOptions_DefaultFormat_IsPdf()
    {
        new PrinterOptions().Format.ShouldBe("Pdf");
    }
}

using DocumentGenerator.Bridge.Configuration;
using FluentAssertions;
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
        BridgeOptions.Section.Should().Be("Bridge");
    }

    [Fact]
    public void BridgeOptions_DefaultPort_Is5100()
    {
        new BridgeOptions().Port.Should().Be(5100);
    }

    [Fact]
    public void BridgeOptions_DefaultIsConfigured_IsFalse()
    {
        new BridgeOptions().IsConfigured.Should().BeFalse();
    }

    // ── CloudOptions ──────────────────────────────────────────────────────────

    [Fact]
    public void CloudOptions_SectionName_IsCloud()
    {
        CloudOptions.Section.Should().Be("Cloud");
    }

    [Fact]
    public void CloudOptions_DefaultBaseUrl_IsEmpty()
    {
        new CloudOptions().BaseUrl.Should().BeEmpty();
    }

    [Fact]
    public void CloudOptions_DefaultApiKey_IsEmpty()
    {
        new CloudOptions().ApiKey.Should().BeEmpty();
    }

    [Fact]
    public void CloudOptions_DefaultTimeout_Is30Seconds()
    {
        new CloudOptions().Timeout.Should().Be(TimeSpan.FromSeconds(30));
    }

    // ── PrinterOptions ────────────────────────────────────────────────────────

    [Fact]
    public void PrinterOptions_SectionName_IsPrinter()
    {
        PrinterOptions.Section.Should().Be("Printer");
    }

    [Fact]
    public void PrinterOptions_DefaultPrinterName_IsNull()
    {
        new PrinterOptions().DefaultPrinterName.Should().BeNull();
    }

    [Fact]
    public void PrinterOptions_DefaultFormat_IsPdf()
    {
        new PrinterOptions().Format.Should().Be("Pdf");
    }
}

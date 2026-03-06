using FluentAssertions;
using DocumentGenerator.Editor.Core.Models;

namespace DocumentGenerator.Editor.Tests.Models;

public class SizePresetTests
{
    // ── GetDimensions ──

    [Fact]
    public void GetDimensions_A6_Returns105x148()
    {
        var dims = SizePreset.A6.GetDimensions();

        dims.Should().NotBeNull();
        dims!.WidthMm.Should().Be(105);
        dims.HeightMm.Should().Be(148);
    }

    [Fact]
    public void GetDimensions_CreditCard_Returns85Point6x54()
    {
        var dims = SizePreset.CreditCard.GetDimensions();

        dims.Should().NotBeNull();
        dims!.WidthMm.Should().Be(85.6);
        dims.HeightMm.Should().Be(54);
    }

    [Fact]
    public void GetDimensions_A4_Returns210x297()
    {
        var dims = SizePreset.A4.GetDimensions();

        dims.Should().NotBeNull();
        dims!.WidthMm.Should().Be(210);
        dims.HeightMm.Should().Be(297);
    }

    [Fact]
    public void GetDimensions_Custom_ReturnsNull()
    {
        var dims = SizePreset.Custom.GetDimensions();
        dims.Should().BeNull();
    }

    // ── FromTemplateName ──

    [Theory]
    [InlineData("badge-pulse-a6", SizePreset.A6)]
    [InlineData("badge-executive-a6", SizePreset.A6)]
    [InlineData("my-template-a6", SizePreset.A6)]
    public void FromTemplateName_A6Suffix_ReturnsA6(string name, SizePreset expected)
    {
        SizePresetExtensions.FromTemplateName(name).Should().Be(expected);
    }

    [Theory]
    [InlineData("badge-carbon-cc", SizePreset.CreditCard)]
    [InlineData("badge-pulse-cc", SizePreset.CreditCard)]
    public void FromTemplateName_CcSuffix_ReturnsCreditCard(string name, SizePreset expected)
    {
        SizePresetExtensions.FromTemplateName(name).Should().Be(expected);
    }

    [Theory]
    [InlineData("report-a4", SizePreset.A4)]
    [InlineData("document-a4", SizePreset.A4)]
    public void FromTemplateName_A4Suffix_ReturnsA4(string name, SizePreset expected)
    {
        SizePresetExtensions.FromTemplateName(name).Should().Be(expected);
    }

    [Theory]
    [InlineData("invoice-basic", SizePreset.A4)]
    [InlineData("invoice-premium", SizePreset.A4)]
    public void FromTemplateName_InvoiceKeyword_ReturnsA4(string name, SizePreset expected)
    {
        SizePresetExtensions.FromTemplateName(name).Should().Be(expected);
    }

    [Theory]
    [InlineData("my-custom-template", SizePreset.Custom)]
    [InlineData("badge-design", SizePreset.Custom)]
    [InlineData("", SizePreset.Custom)]
    public void FromTemplateName_NoMatch_ReturnsCustom(string name, SizePreset expected)
    {
        SizePresetExtensions.FromTemplateName(name).Should().Be(expected);
    }

    [Fact]
    public void FromTemplateName_Null_Throws()
    {
        var act = () => SizePresetExtensions.FromTemplateName(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void FromTemplateName_CaseInsensitive()
    {
        SizePresetExtensions.FromTemplateName("Badge-Pulse-A6").Should().Be(SizePreset.A6);
        SizePresetExtensions.FromTemplateName("BADGE-CARBON-CC").Should().Be(SizePreset.CreditCard);
    }

    // ── Dimensions.ToString ──

    [Fact]
    public void Dimensions_ToString_FormatsCorrectly()
    {
        var dims = new Dimensions(105, 148);
        dims.ToString().Should().Contain("105").And.Contain("148").And.Contain("mm");
    }
}

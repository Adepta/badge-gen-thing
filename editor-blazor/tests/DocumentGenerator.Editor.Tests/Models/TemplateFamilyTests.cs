using FluentAssertions;
using DocumentGenerator.Editor.Core.Models;

namespace DocumentGenerator.Editor.Tests.Models;

public class TemplateFamilyTests
{
    [Theory]
    [InlineData("badge-pulse-a6", TemplateFamily.Pulse)]
    [InlineData("pulse-design", TemplateFamily.Pulse)]
    [InlineData("my-pulse-template", TemplateFamily.Pulse)]
    public void FromTemplateName_PulseKeyword_ReturnsPulse(string name, TemplateFamily expected)
    {
        TemplateFamilyExtensions.FromTemplateName(name).Should().Be(expected);
    }

    [Theory]
    [InlineData("badge-executive-cc", TemplateFamily.Executive)]
    [InlineData("executive-badge", TemplateFamily.Executive)]
    public void FromTemplateName_ExecutiveKeyword_ReturnsExecutive(string name, TemplateFamily expected)
    {
        TemplateFamilyExtensions.FromTemplateName(name).Should().Be(expected);
    }

    [Theory]
    [InlineData("badge-carbon-a6", TemplateFamily.Carbon)]
    [InlineData("carbon-dark", TemplateFamily.Carbon)]
    public void FromTemplateName_CarbonKeyword_ReturnsCarbon(string name, TemplateFamily expected)
    {
        TemplateFamilyExtensions.FromTemplateName(name).Should().Be(expected);
    }

    [Theory]
    [InlineData("invoice-basic", TemplateFamily.Invoice)]
    [InlineData("invoice", TemplateFamily.Invoice)]
    public void FromTemplateName_InvoiceKeyword_ReturnsInvoice(string name, TemplateFamily expected)
    {
        TemplateFamilyExtensions.FromTemplateName(name).Should().Be(expected);
    }

    [Theory]
    [InlineData("my-template", TemplateFamily.Custom)]
    [InlineData("badge-design-a6", TemplateFamily.Custom)]
    [InlineData("", TemplateFamily.Custom)]
    public void FromTemplateName_NoMatch_ReturnsCustom(string name, TemplateFamily expected)
    {
        TemplateFamilyExtensions.FromTemplateName(name).Should().Be(expected);
    }

    [Fact]
    public void FromTemplateName_Null_Throws()
    {
        var act = () => TemplateFamilyExtensions.FromTemplateName(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void FromTemplateName_CaseInsensitive()
    {
        TemplateFamilyExtensions.FromTemplateName("Badge-PULSE-a6").Should().Be(TemplateFamily.Pulse);
        TemplateFamilyExtensions.FromTemplateName("CARBON-design").Should().Be(TemplateFamily.Carbon);
    }

    [Fact]
    public void FromTemplateName_PriorityOrder_PulseBeforeCarbon()
    {
        // "pulse" is checked before "carbon"
        TemplateFamilyExtensions.FromTemplateName("pulse-carbon-mix").Should().Be(TemplateFamily.Pulse);
    }
}

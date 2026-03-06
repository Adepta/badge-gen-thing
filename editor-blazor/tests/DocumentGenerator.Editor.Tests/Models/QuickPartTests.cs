using FluentAssertions;
using DocumentGenerator.Editor.Core.Models;

namespace DocumentGenerator.Editor.Tests.Models;

public class QuickPartTests
{
    [Fact]
    public void All_IsNotEmpty()
    {
        QuickPart.All.Should().NotBeEmpty();
    }

    [Fact]
    public void All_HasMinimumCount()
    {
        // Expect at least 25 quick parts (tokens + blocks)
        QuickPart.All.Count.Should().BeGreaterThanOrEqualTo(25);
    }

    [Fact]
    public void All_AllItemsHaveRequiredFields()
    {
        foreach (var qp in QuickPart.All)
        {
            qp.Label.Should().NotBeNullOrWhiteSpace($"Label is required for all quick parts");
            qp.InsertText.Should().NotBeNullOrWhiteSpace($"InsertText is required for {qp.Label}");
            qp.Category.Should().NotBeNullOrWhiteSpace($"Category is required for {qp.Label}");
            qp.Group.Should().NotBeNullOrWhiteSpace($"Group is required for {qp.Label}");
        }
    }

    [Fact]
    public void All_ContainsTokenCategory()
    {
        QuickPart.All.Should().Contain(qp => qp.Category == "Token");
    }

    [Fact]
    public void All_ContainsBlockCategory()
    {
        QuickPart.All.Should().Contain(qp => qp.Category == "Block");
    }

    [Fact]
    public void All_ContainsAttendeeGroup()
    {
        QuickPart.All.Should().Contain(qp => qp.Group == "Attendee");
    }

    [Fact]
    public void All_ContainsBrandingGroup()
    {
        QuickPart.All.Should().Contain(qp => qp.Group == "Branding");
    }

    [Fact]
    public void All_ContainsHelpersGroup()
    {
        QuickPart.All.Should().Contain(qp => qp.Group == "Helpers");
    }

    [Fact]
    public void All_ContainsCssTargetedItems()
    {
        QuickPart.All.Should().Contain(qp => qp.TargetEditor == QuickPartTarget.Css);
    }

    [Fact]
    public void All_ContainsHtmlTargetedItems()
    {
        QuickPart.All.Should().Contain(qp => qp.TargetEditor == QuickPartTarget.Html);
    }

    [Fact]
    public void All_IsCachedSingleton()
    {
        var a = QuickPart.All;
        var b = QuickPart.All;
        a.Should().BeSameAs(b);
    }

    [Fact]
    public void All_FirstNameTokenExists()
    {
        QuickPart.All.Should().Contain(qp =>
            qp.InsertText == "{{variables.firstName}}" &&
            qp.Category == "Token" &&
            qp.Group == "Attendee");
    }
}

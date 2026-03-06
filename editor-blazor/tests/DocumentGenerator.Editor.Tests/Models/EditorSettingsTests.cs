using FluentAssertions;
using DocumentGenerator.Editor.Core.Models;

namespace DocumentGenerator.Editor.Tests.Models;

public class EditorSettingsTests
{
    [Fact]
    public void Defaults_AreCorrect()
    {
        var settings = new EditorSettings();

        settings.Theme.Should().Be("dark");
        settings.EditorLayout.Should().Be(EditorLayout.Vertical);
        settings.TabSize.Should().Be(2);
        settings.WordWrap.Should().BeFalse();
        settings.Minimap.Should().BeFalse();
        settings.LineNumbers.Should().BeTrue();
        settings.FontSize.Should().Be(13);
        settings.PreviewDefaultSize.Should().Be(SizePreset.A6);
        settings.AutoRefreshPreview.Should().BeTrue();
        settings.PreviewDebounceMs.Should().Be(280);
    }

    [Fact]
    public void Properties_AreSettable()
    {
        var settings = new EditorSettings
        {
            Theme = "light",
            EditorLayout = EditorLayout.Horizontal,
            TabSize = 4,
            WordWrap = true,
            Minimap = true,
            LineNumbers = false,
            FontSize = 16,
            PreviewDefaultSize = SizePreset.A4,
            AutoRefreshPreview = false,
            PreviewDebounceMs = 500
        };

        settings.Theme.Should().Be("light");
        settings.EditorLayout.Should().Be(EditorLayout.Horizontal);
        settings.TabSize.Should().Be(4);
        settings.WordWrap.Should().BeTrue();
        settings.Minimap.Should().BeTrue();
        settings.LineNumbers.Should().BeFalse();
        settings.FontSize.Should().Be(16);
        settings.PreviewDefaultSize.Should().Be(SizePreset.A4);
        settings.AutoRefreshPreview.Should().BeFalse();
        settings.PreviewDebounceMs.Should().Be(500);
    }
}

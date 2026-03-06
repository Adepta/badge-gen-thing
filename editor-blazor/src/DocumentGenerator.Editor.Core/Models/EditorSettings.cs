namespace DocumentGenerator.Editor.Core.Models;

/// <summary>
/// Layout arrangement for the HTML / CSS editors.
/// </summary>
public enum EditorLayout
{
    /// <summary>HTML above CSS (default).</summary>
    Vertical,

    /// <summary>HTML left, CSS right.</summary>
    Horizontal,

    /// <summary>Tabbed view – one editor visible at a time.</summary>
    Tabbed
}

/// <summary>
/// Persisted user preferences for the editor UI.
/// </summary>
public class EditorSettings
{
    /// <summary>Colour theme: "dark", "light", or "system".</summary>
    public string Theme { get; set; } = "dark";

    /// <summary>Layout arrangement for the HTML / CSS editors.</summary>
    public EditorLayout EditorLayout { get; set; } = EditorLayout.Vertical;

    /// <summary>Number of spaces per tab stop in the code editor.</summary>
    public int TabSize { get; set; } = 2;

    /// <summary>Whether long lines should wrap in the editor.</summary>
    public bool WordWrap { get; set; } = false;

    /// <summary>Whether the minimap is shown in the editor gutter.</summary>
    public bool Minimap { get; set; } = false;

    /// <summary>Whether line numbers are displayed.</summary>
    public bool LineNumbers { get; set; } = true;

    /// <summary>Editor font size in pixels.</summary>
    public int FontSize { get; set; } = 13;

    /// <summary>Default preview canvas size.</summary>
    public SizePreset PreviewDefaultSize { get; set; } = SizePreset.A6;

    /// <summary>Whether the preview auto-refreshes on content changes.</summary>
    public bool AutoRefreshPreview { get; set; } = true;

    /// <summary>Debounce delay in milliseconds before the preview refreshes.</summary>
    public int PreviewDebounceMs { get; set; } = 280;
}

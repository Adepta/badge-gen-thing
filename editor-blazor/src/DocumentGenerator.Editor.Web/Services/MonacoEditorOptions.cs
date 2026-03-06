namespace DocumentGenerator.Editor.Web.Services;

/// <summary>
/// Options for configuring a Monaco Editor instance.
/// </summary>
public class MonacoEditorOptions
{
    /// <summary>Monaco theme name (e.g., "editor-dark", "editor-light").</summary>
    public string Theme { get; set; } = "editor-dark";

    /// <summary>Font size in pixels.</summary>
    public int FontSize { get; set; } = 13;

    /// <summary>Whether the minimap is visible.</summary>
    public bool Minimap { get; set; } = false;

    /// <summary>Whether long lines should wrap.</summary>
    public bool WordWrap { get; set; } = false;

    /// <summary>Number of spaces per tab.</summary>
    public int TabSize { get; set; } = 2;

    /// <summary>Whether line numbers are displayed.</summary>
    public bool LineNumbers { get; set; } = true;
}

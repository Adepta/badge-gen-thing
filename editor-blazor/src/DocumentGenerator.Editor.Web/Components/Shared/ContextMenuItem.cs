using Microsoft.AspNetCore.Components;

namespace DocumentGenerator.Editor.Web.Components.Shared;

/// <summary>
/// Represents a single item in a context menu.
/// </summary>
public class ContextMenuItem
{
    /// <summary>
    /// Optional icon rendered before the label (a RenderFragment, e.g. an SVG).
    /// </summary>
    public RenderFragment? Icon { get; set; }

    /// <summary>
    /// Display label for the menu item.
    /// </summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// Async action to invoke when the item is clicked.
    /// </summary>
    public Func<Task>? Action { get; set; }

    /// <summary>
    /// Whether this item represents a destructive action (shown in red).
    /// </summary>
    public bool IsDestructive { get; set; }
}

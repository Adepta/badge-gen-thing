namespace DocumentGenerator.Core.Models;

/// <summary>
/// Per-document PDF rendering options. All properties are optional and
/// fall back to sensible defaults in the rendering layer.
/// </summary>
public sealed class PdfOptions
{
    /// <summary>Paper format, e.g. "A4", "Letter". Ignored when Width and Height are set.</summary>
    public string Format { get; init; } = "A4";

    /// <summary>Custom page width in CSS units, e.g. "85.6mm". Overrides Format when set.</summary>
    public string? Width { get; init; }

    /// <summary>Custom page height in CSS units, e.g. "54mm". Overrides Format when set.</summary>
    public string? Height { get; init; }

    /// <summary>Page orientation. Defaults to portrait.</summary>
    public bool Landscape { get; init; } = false;

    /// <summary>Print background graphics. Defaults to true.</summary>
    public bool PrintBackground { get; init; } = true;

    /// <summary>Page margins in CSS units, e.g. "10mm". Null uses Chromium defaults.</summary>
    public PdfMargins? Margins { get; init; }

    /// <summary>Display header template (Handlebars-processed HTML).</summary>
    public string? HeaderTemplate { get; init; }

    /// <summary>Display footer template (Handlebars-processed HTML).</summary>
    public string? FooterTemplate { get; init; }

    /// <summary>Scale factor for the webpage rendering. Must be between 0.1 and 2.</summary>
    public double Scale { get; init; } = 1.0;
}

/// <summary>
/// Page margin values in CSS units (e.g. <c>"10mm"</c>, <c>"0"</c>).
/// Any null dimension falls back to the Chromium default for that side.
/// </summary>
public sealed class PdfMargins
{
    /// <summary>Top margin, e.g. <c>"10mm"</c>.</summary>
    public string? Top { get; init; }

    /// <summary>Bottom margin, e.g. <c>"10mm"</c>.</summary>
    public string? Bottom { get; init; }

    /// <summary>Left margin, e.g. <c>"10mm"</c>.</summary>
    public string? Left { get; init; }

    /// <summary>Right margin, e.g. <c>"10mm"</c>.</summary>
    public string? Right { get; init; }
}

using DocumentGenerator.Editor.Core.Models;

namespace DocumentGenerator.Editor.Core.DTOs;

/// <summary>
/// Request payload for saving a template.
/// </summary>
/// <param name="Name">Template name (file stem).</param>
/// <param name="HtmlContent">HTML / Handlebars content.</param>
/// <param name="CssContent">CSS content.</param>
/// <param name="SampleData">Optional sample data to persist alongside the template.</param>
public record TemplateSaveRequest(
    string Name,
    string HtmlContent,
    string CssContent,
    SampleData? SampleData = null);

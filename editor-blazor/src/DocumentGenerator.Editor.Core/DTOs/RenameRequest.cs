namespace DocumentGenerator.Editor.Core.DTOs;

/// <summary>
/// Request payload for renaming a template.
/// </summary>
/// <param name="OldName">Current template name.</param>
/// <param name="NewName">Desired new template name.</param>
public record RenameRequest(string OldName, string NewName);

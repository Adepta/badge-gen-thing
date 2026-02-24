namespace DocumentGenerator.Core.Models;

/// <summary>
/// A self-contained render job. This is the unit of work passed between
/// services — whether it arrived from the console, an HTTP endpoint,
/// or a queue message.
/// </summary>
public sealed class RenderRequest
{
    /// <summary>Unique identifier for this job — useful for logging and tracing.</summary>
    public Guid JobId { get; init; } = Guid.NewGuid();

    /// <summary>The fully resolved template including branding and variables.</summary>
    public required DocumentTemplate Template { get; init; }

    /// <summary>UTC timestamp when the request was created.</summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

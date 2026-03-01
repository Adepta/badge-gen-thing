namespace DocumentGenerator.Core.Errors;

/// <summary>
/// Base exception for all document-generation errors.
///
/// Every exception thrown deliberately within this solution derives from
/// <see cref="DocumentGeneratorException"/> so callers can distinguish
/// domain errors from unexpected framework exceptions.
///
/// Each exception carries a machine-readable <see cref="ErrorCode"/> that
/// can be logged, surfaced in API responses, and matched in tests without
/// coupling to string messages.
/// </summary>
public class DocumentGeneratorException : Exception
{
    /// <summary>Machine-readable error code identifying the failure category.</summary>
    public ErrorCode Code { get; }

    /// <summary>
    /// Additional context, e.g. template name, correlation ID, file path.
    /// Included in log messages and error responses to help operators diagnose problems.
    /// </summary>
    public IReadOnlyDictionary<string, object?> Context { get; }

    /// <summary>Initialises a new exception with an error code and message.</summary>
    public DocumentGeneratorException(ErrorCode code, string message)
        : base(message)
    {
        Code    = code;
        Context = new Dictionary<string, object?>();
    }

    /// <summary>Initialises a new exception with an error code, message, and inner exception.</summary>
    public DocumentGeneratorException(ErrorCode code, string message, Exception innerException)
        : base(message, innerException)
    {
        Code    = code;
        Context = new Dictionary<string, object?>();
    }

    /// <summary>Initialises a new exception with an error code, message, and diagnostic context.</summary>
    public DocumentGeneratorException(
        ErrorCode code,
        string message,
        IReadOnlyDictionary<string, object?> context)
        : base(message)
    {
        Code    = code;
        Context = context;
    }

    /// <summary>
    /// Initialises a new exception with an error code, message, diagnostic context,
    /// and an inner exception.
    /// </summary>
    public DocumentGeneratorException(
        ErrorCode code,
        string message,
        IReadOnlyDictionary<string, object?> context,
        Exception innerException)
        : base(message, innerException)
    {
        Code    = code;
        Context = context;
    }

    /// <summary>
    /// Returns a string that includes the error code prefix, e.g.:
    /// <c>[DG1001] Badge template 'badge-pulse-a6' not found.</c>
    /// </summary>
    public override string ToString() =>
        $"[DG{(int)Code:D4}] {Message}{(InnerException is not null ? $" --> {InnerException.Message}" : string.Empty)}";
}

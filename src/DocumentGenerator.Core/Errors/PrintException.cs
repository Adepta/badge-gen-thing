namespace DocumentGenerator.Core.Errors;

/// <summary>
/// Thrown when a Bridge-side print or cloud-render operation fails.
/// </summary>
public sealed class PrintException : DocumentGeneratorException
{
    /// <summary>The printer name involved, if applicable.</summary>
    public string? PrinterName { get; }

    private PrintException(
        ErrorCode code,
        string message,
        string? printerName = null,
        Exception? inner = null)
        : base(
            code,
            message,
            BuildContext(printerName),
            inner!)
    {
        PrinterName = printerName;
    }

    // ── Factory methods ───────────────────────────────────────────────────────

    /// <summary>
    /// Creates a <see cref="ErrorCode.CloudRenderFailed"/> exception when the
    /// Bridge cannot obtain a rendered document from the cloud API.
    /// </summary>
    public static PrintException CloudRenderFailed(string? detail, Exception? inner = null) =>
        new(ErrorCode.CloudRenderFailed,
            $"Cloud render request failed: {detail ?? "unknown error"}.",
            null, inner);

    /// <summary>
    /// Creates a <see cref="ErrorCode.CloudResponseDecodeFailed"/> exception when the
    /// Base64 document payload cannot be decoded.
    /// </summary>
    public static PrintException DecodeFailed(Exception inner) =>
        new(ErrorCode.CloudResponseDecodeFailed,
            "Failed to decode Base64 document payload from the cloud API.",
            null, inner);

    /// <summary>
    /// Creates a <see cref="ErrorCode.PrintSpoolerFailed"/> exception when the local
    /// print spooler rejects or fails the job.
    /// </summary>
    public static PrintException SpoolerFailed(string printerName, string? detail, Exception? inner = null) =>
        new(ErrorCode.PrintSpoolerFailed,
            $"Print spooler failed for printer '{printerName}': {detail ?? "unknown error"}.",
            printerName, inner);

    /// <summary>
    /// Creates a <see cref="ErrorCode.PrintProcessTimeout"/> exception when the print
    /// helper process does not exit within the allowed time.
    /// </summary>
    public static PrintException ProcessTimeout(string printerName, int timeoutMs) =>
        new(ErrorCode.PrintProcessTimeout,
            $"Print process did not exit within {timeoutMs}ms for printer '{printerName}'.",
            printerName);

    /// <summary>
    /// Creates a <see cref="ErrorCode.PrintHelperNotFound"/> exception when no
    /// suitable PDF viewer can be located.
    /// </summary>
    public static PrintException HelperNotFound() =>
        new(ErrorCode.PrintHelperNotFound,
            "No PDF print helper (SumatraPDF, Edge, Acrobat) was found on this machine. " +
            "Install SumatraPDF or configure Printer:SumatraPdfPath.");

    // ─────────────────────────────────────────────────────────────────────────

    private static IReadOnlyDictionary<string, object?> BuildContext(string? printerName) =>
        new Dictionary<string, object?> { ["printerName"] = printerName };
}

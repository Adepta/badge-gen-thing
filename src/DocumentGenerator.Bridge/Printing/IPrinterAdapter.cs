namespace DocumentGenerator.Bridge.Printing;

/// <summary>
/// Abstraction over a local printer. Implementations handle OS-specific
/// print spooler APIs so the rest of the bridge remains platform-agnostic.
/// </summary>
/// <remarks>
/// Two adapters are provided out of the box:
/// <list type="bullet">
///   <item><see cref="WindowsPrinterAdapter"/> — uses <c>System.Drawing.Printing</c> on Windows.</item>
///   <item><see cref="CupsPrinterAdapter"/> — shells out to <c>lp</c> on Linux and macOS.</item>
/// </list>
/// The correct adapter is selected at startup via <see cref="PrinterAdapterFactory"/>.
/// </remarks>
public interface IPrinterAdapter
{
    /// <summary>
    /// Returns the names of all printers currently visible to the OS.
    /// </summary>
    /// <returns>An ordered sequence of printer display names.</returns>
    IEnumerable<string> GetAvailablePrinters();

    /// <summary>
    /// Sends raw document bytes to the specified printer via the OS print spooler.
    /// </summary>
    /// <param name="documentBytes">PDF or PNG bytes to print.</param>
    /// <param name="mimeType">
    /// MIME type of the document: <c>application/pdf</c> or <c>image/png</c>.
    /// </param>
    /// <param name="printerName">
    /// Target printer name. When <c>null</c> or empty, the OS default printer is used.
    /// </param>
    /// <param name="jobName">
    /// Display name shown in the print queue, e.g. <c>"Badge – Jane Smith"</c>.
    /// </param>
    /// <param name="cancellationToken">Token to cancel the print operation.</param>
    /// <returns>
    /// A <see cref="PrintResult"/> describing whether the job was submitted successfully.
    /// </returns>
    Task<PrintResult> PrintAsync(
        byte[]            documentBytes,
        string            mimeType,
        string?           printerName,
        string            jobName,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// The outcome of a print job submission.
/// </summary>
public sealed class PrintResult
{
    /// <summary><c>true</c> when the job was accepted by the print spooler.</summary>
    public bool Success { get; init; }

    /// <summary>
    /// The printer name actually used (resolves the OS default when input was null).
    /// </summary>
    public string? PrinterUsed { get; init; }

    /// <summary>Human-readable error when <see cref="Success"/> is <c>false</c>.</summary>
    public string? Error { get; init; }

    /// <summary>
    /// Machine-readable error code (e.g. <c>DG5003</c>) when <see cref="Success"/> is <c>false</c>.
    /// </summary>
    public string? ErrorCode { get; init; }

    /// <summary>Creates a successful <see cref="PrintResult"/>.</summary>
    public static PrintResult Ok(string printerUsed) =>
        new() { Success = true, PrinterUsed = printerUsed };

    /// <summary>Creates a failed <see cref="PrintResult"/>.</summary>
    public static PrintResult Fail(string error, string? printerUsed = null, string? errorCode = null) =>
        new() { Success = false, Error = error, PrinterUsed = printerUsed, ErrorCode = errorCode };
}

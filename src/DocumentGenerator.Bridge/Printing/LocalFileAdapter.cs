namespace DocumentGenerator.Bridge.Printing;

/// <summary>
/// Development/test <see cref="IPrinterAdapter"/> that writes documents to
/// a local <c>Generated/</c> folder instead of sending them to an OS print spooler.
/// </summary>
/// <remarks>
/// Registered automatically when the application runs in the <c>Development</c>
/// environment (see <see cref="PrinterAdapterFactory"/>).  This means:
/// <list type="bullet">
///   <item>No physical printer is required during local development or CI.</item>
///   <item>Output files can be inspected directly in <c>Generated/</c>.</item>
///   <item>The full render → print code path is exercised end-to-end.</item>
/// </list>
/// Files are named <c>{jobName}_{timestamp}.{ext}</c> so successive prints
/// don't overwrite each other.
/// </remarks>
public sealed class LocalFileAdapter : IPrinterAdapter
{
    /// <summary>
    /// Pseudo-printer name reported back in <see cref="PrintResult"/> and in
    /// the <c>/printers</c> endpoint response.
    /// </summary>
    public const string PrinterName = "LocalFile (Development)";

    private readonly string _outputDir;
    private readonly ILogger<LocalFileAdapter> _logger;

    /// <summary>
    /// Initialises a new <see cref="LocalFileAdapter"/>.
    /// </summary>
    /// <param name="logger">Logger for file-write diagnostics.</param>
    public LocalFileAdapter(ILogger<LocalFileAdapter> logger)
    {
        _logger = logger;

        // Resolve Generated/ relative to the solution root (two levels up from the
        // binary output directory src/DocumentGenerator.Bridge/bin/<cfg>/net10.0/).
        // Falls back to AppContext.BaseDirectory/Generated/ if the path can't be found.
        var binDir      = AppContext.BaseDirectory;
        var solutionDir = FindSolutionRoot(binDir) ?? binDir;
        _outputDir      = Path.Combine(solutionDir, "Generated");

        Directory.CreateDirectory(_outputDir);
        _logger.LogInformation("LocalFileAdapter: print output → {OutputDir}", _outputDir);
    }

    /// <inheritdoc />
    public IEnumerable<string> GetAvailablePrinters() => [PrinterName];

    /// <inheritdoc />
    public async Task<PrintResult> PrintAsync(
        byte[]            documentBytes,
        string            mimeType,
        string?           printerName,
        string            jobName,
        CancellationToken cancellationToken = default)
    {
        var ext      = mimeType.Contains("pdf", StringComparison.OrdinalIgnoreCase) ? "pdf" : "png";
        var safeName = string.Concat(jobName.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        var fileName = $"{safeName}_{DateTimeOffset.UtcNow:yyyyMMdd_HHmmss}.{ext}";
        var filePath = Path.Combine(_outputDir, fileName);

        _logger.LogInformation(
            "LocalFileAdapter: writing {Bytes} bytes → {FilePath}",
            documentBytes.Length, filePath);

        await File.WriteAllBytesAsync(filePath, documentBytes, cancellationToken);

        return PrintResult.Ok(PrinterName);
    }

    // Walk up from the binary output directory looking for the .sln file.
    private static string? FindSolutionRoot(string startDir)
    {
        var dir = new DirectoryInfo(startDir);
        while (dir is not null)
        {
            if (dir.EnumerateFiles("*.sln").Any())
                return dir.FullName;
            dir = dir.Parent;
        }
        return null;
    }
}

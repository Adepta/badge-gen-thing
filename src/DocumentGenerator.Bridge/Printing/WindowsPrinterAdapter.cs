using System.Diagnostics;
using System.Drawing.Printing;
using System.Runtime.Versioning;
using DocumentGenerator.Core.Errors;

namespace DocumentGenerator.Bridge.Printing;

/// <summary>
/// <see cref="IPrinterAdapter"/> implementation for Windows.
/// Uses <c>System.Drawing.Printing</c> to enumerate printers and a
/// priority-ordered set of strategies to physically submit the print job.
/// </summary>
/// <remarks>
/// <para>
/// PDF printing strategy (evaluated in order):
/// <list type="number">
///   <item>
///     <b>SumatraPDF</b> — <c>SumatraPDF.exe -print-to "&lt;printer&gt;" &lt;file&gt;</c>.
///     Silent, headless, no UI.  Preferred when SumatraPDF is installed.
///   </item>
///   <item>
///     <b>Microsoft Edge kiosk</b> — launches Edge with <c>--kiosk --kiosk-printing</c>
///     pointing at the temp file URI.  Works on all modern Windows 10/11 machines.
///   </item>
///   <item>
///     <b>Shell <c>printto</c> verb</b> — requires the machine to have a PDF viewer
///     registered for the <c>printto</c> verb (e.g. Adobe Acrobat, Foxit).
///     Included as a last resort; may fail with error 1155 when no viewer is installed.
///   </item>
/// </list>
/// </para>
/// <para>PNG files always use the shell <c>printto</c> verb (mspaint handles them silently).</para>
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class WindowsPrinterAdapter(ILogger<WindowsPrinterAdapter> logger) : IPrinterAdapter, IDisposable
{
    private readonly ILogger<WindowsPrinterAdapter> _logger = logger;

    /// <inheritdoc />
    public IEnumerable<string> GetAvailablePrinters()
    {
        var printers = new List<string>();
        foreach (string printer in PrinterSettings.InstalledPrinters)
            printers.Add(printer);
        return printers.OrderBy(p => p);
    }

    /// <inheritdoc />
    public async Task<PrintResult> PrintAsync(
        byte[]            documentBytes,
        string            mimeType,
        string?           printerName,
        string            jobName,
        CancellationToken cancellationToken = default)
    {
        var resolvedPrinter = ResolveDefaultPrinter(printerName);
        var isPdf    = mimeType.Contains("pdf", StringComparison.OrdinalIgnoreCase);
        var ext      = isPdf ? ".pdf" : ".png";
        var tempFile = Path.Combine(Path.GetTempPath(), $"badge_{Guid.NewGuid():N}{ext}");

        _logger.LogInformation(
            "Windows print — Printer={Printer} MimeType={MimeType} Bytes={Bytes}",
            resolvedPrinter, mimeType, documentBytes.Length);

        try
        {
            await File.WriteAllBytesAsync(tempFile, documentBytes, cancellationToken);

            if (isPdf)
                await PrintPdfAsync(tempFile, resolvedPrinter, cancellationToken);
            else
                await ShellVerbAsync(tempFile, resolvedPrinter, cancellationToken);

            _logger.LogInformation("Print job submitted — Printer={Printer}", resolvedPrinter);
            return PrintResult.Ok(resolvedPrinter);
        }
        catch (PrintException ex)
        {
            _logger.LogError(ex,
                "[{ErrorCode}] Windows print failed — Printer={Printer}",
                ex.ToString(), resolvedPrinter);
            return PrintResult.Fail(ex.Message, resolvedPrinter, ex.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Windows print failed — Printer={Printer}", resolvedPrinter);
            return PrintResult.Fail(ex.Message, resolvedPrinter);
        }
        finally
        {
            // Small delay before delete so the print process has opened the file
            await Task.Delay(500, CancellationToken.None);
            TryDeleteFile(tempFile);
        }
    }

    // ── PDF printing ──────────────────────────────────────────────────────────

    private async Task PrintPdfAsync(string filePath, string printerName, CancellationToken ct)
    {
        // Strategy 1: SumatraPDF (best — silent, CLI-native PDF printer)
        var sumatraPath = FindSumatraPdf();
        if (sumatraPath is not null)
        {
            _logger.LogDebug("Printing via SumatraPDF: {Path}", sumatraPath);
            await RunProcessAsync(
                sumatraPath,
                $"-print-to \"{printerName}\" -silent \"{filePath}\"",
                timeoutMs: 30_000,
                ct);
            return;
        }

        // Strategy 2: Microsoft Edge kiosk printing (always present on Win10/11)
        var edgePath = FindEdgePath();
        if (edgePath is not null)
        {
            _logger.LogDebug("Printing via Microsoft Edge kiosk: {Path}", edgePath);
            // Edge kiosk mode with --kiosk-printing prints without a dialog.
            // We use the file:// URI and set the default printer via registry at
            // startup — here we just target the named printer via Edge's flag.
            var fileUri = new Uri(filePath).AbsoluteUri;
            await RunProcessAsync(
                edgePath,
                $"--headless=new --disable-gpu --print-to=\"{printerName}\" --no-pdf-header-footer \"{fileUri}\"",
                timeoutMs: 30_000,
                ct);
            return;
        }

        // Strategy 3: Shell printto verb (requires registered PDF viewer)
        _logger.LogDebug("Printing via shell printto verb (fallback)");
        await ShellVerbAsync(filePath, printerName, ct);
    }

    // ── Shell verb ────────────────────────────────────────────────────────────

    private static async Task ShellVerbAsync(string filePath, string printerName, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName        = filePath,
            Verb            = "printto",
            Arguments       = $"\"{printerName}\"",
            CreateNoWindow  = true,
            UseShellExecute = true,
            WindowStyle     = ProcessWindowStyle.Hidden
        };

        var process = Process.Start(psi)
            ?? throw PrintException.SpoolerFailed("(shell)", "Failed to start shell printto process.");

        await process.WaitForExitAsync(ct);
    }

    // ── Process helpers ───────────────────────────────────────────────────────

    private static async Task RunProcessAsync(string exe, string args, int timeoutMs, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName               = exe,
            Arguments              = args,
            CreateNoWindow         = true,
            UseShellExecute        = false,
            RedirectStandardOutput = true,
            RedirectStandardError  = true
        };

        var process = Process.Start(psi)
            ?? throw PrintException.SpoolerFailed(exe, $"Failed to start process: {exe}");

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(timeoutMs);

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // Timed out — kill the process and treat as success (job may have been
            // submitted to the spooler even if the viewer didn't exit cleanly).
            // Log but do not rethrow — the print job was likely already spooled.
            try { process.Kill(entireProcessTree: true); } catch { /* best-effort */ }
            throw PrintException.ProcessTimeout(exe, timeoutMs);
        }
    }

    // ── Discovery helpers ─────────────────────────────────────────────────────

    private static string? FindSumatraPdf()
    {
        string[] candidates =
        [
            @"C:\Program Files\SumatraPDF\SumatraPDF.exe",
            @"C:\Program Files (x86)\SumatraPDF\SumatraPDF.exe",
        ];

        var fromPath = FindOnPath("SumatraPDF.exe") ?? FindOnPath("SumatraPDF");
        return fromPath ?? candidates.FirstOrDefault(File.Exists);
    }

    private static string? FindEdgePath()
    {
        string[] candidates =
        [
            @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
            @"C:\Program Files\Microsoft\Edge\Application\msedge.exe",
        ];
        return candidates.FirstOrDefault(File.Exists);
    }

    private static string? FindOnPath(string exe)
    {
        var paths = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator) ?? [];
        return paths.Select(p => Path.Combine(p, exe)).FirstOrDefault(File.Exists);
    }

    private static string ResolveDefaultPrinter(string? printerName)
    {
        if (!string.IsNullOrWhiteSpace(printerName))
            return printerName;

        var settings = new PrinterSettings();
        return settings.PrinterName;
    }

    private void TryDeleteFile(string path)
    {
        try { File.Delete(path); }
        catch (Exception ex) { _logger.LogWarning(ex, "Could not delete temp print file: {Path}", path); }
    }

    /// <inheritdoc />
    public void Dispose() { /* nothing managed to release */ }
}

using System.Diagnostics;
using System.Runtime.Versioning;

namespace DocumentGenerator.Bridge.Printing;

/// <summary>
/// <see cref="IPrinterAdapter"/> implementation for Linux and macOS.
/// Uses the CUPS <c>lp</c> command-line utility to submit print jobs,
/// which natively handles both PDF and PNG documents.
/// </summary>
/// <remarks>
/// <c>lp</c> is available by default on macOS and on most Linux distributions
/// that have CUPS installed (e.g. <c>apt install cups</c> on Debian/Ubuntu).
/// </remarks>
[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macos")]
public sealed class CupsPrinterAdapter : IPrinterAdapter
{
    private readonly ILogger<CupsPrinterAdapter> _logger;

    /// <summary>
    /// Initialises a new <see cref="CupsPrinterAdapter"/>.
    /// </summary>
    /// <param name="logger">Logger for print job diagnostics.</param>
    public CupsPrinterAdapter(ILogger<CupsPrinterAdapter> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public IEnumerable<string> GetAvailablePrinters()
    {
        try
        {
            // `lpstat -a` lists all accepting printer queues, one per line: "PrinterName accepting..."
            var output = RunCommand("lpstat", "-a");
            return output
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Split(' ')[0].Trim())
                .Where(name => !string.IsNullOrEmpty(name))
                .OrderBy(name => name);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not enumerate CUPS printers via lpstat");
            return [];
        }
    }

    /// <inheritdoc />
    public async Task<PrintResult> PrintAsync(
        byte[]            documentBytes,
        string            mimeType,
        string?           printerName,
        string            jobName,
        CancellationToken cancellationToken = default)
    {
        var ext      = mimeType.Contains("pdf", StringComparison.OrdinalIgnoreCase) ? ".pdf" : ".png";
        var tempFile = Path.Combine(Path.GetTempPath(), $"badge_{Guid.NewGuid():N}{ext}");

        try
        {
            await File.WriteAllBytesAsync(tempFile, documentBytes, cancellationToken);

            // Build lp arguments: [-d printer] [-t jobname] <file>
            var args = BuildLpArgs(tempFile, printerName, jobName);

            _logger.LogInformation(
                "CUPS print — Args={Args} MimeType={MimeType} Bytes={Bytes}",
                args, mimeType, documentBytes.Length);

            var output = await RunCommandAsync("lp", args, cancellationToken);

            // `lp` outputs "request id is <printer>-<job> (1 file(s))" on success
            var resolvedPrinter = printerName ?? ResolveDefaultPrinter();
            _logger.LogDebug("lp output: {Output}", output);

            return PrintResult.Ok(resolvedPrinter);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CUPS print failed — Printer={Printer}", printerName);
            return PrintResult.Fail(ex.Message, printerName);
        }
        finally
        {
            TryDeleteFile(tempFile);
        }
    }

    private static string BuildLpArgs(string filePath, string? printerName, string jobName)
    {
        var args = new List<string>();

        if (!string.IsNullOrWhiteSpace(printerName))
        {
            args.Add("-d");
            args.Add($"\"{printerName}\"");
        }

        args.Add("-t");
        args.Add($"\"{jobName}\"");
        args.Add($"\"{filePath}\"");

        return string.Join(' ', args);
    }

    private static string ResolveDefaultPrinter()
    {
        try
        {
            // `lpstat -d` returns "system default destination: <name>"
            var output = RunCommand("lpstat", "-d");
            var parts  = output.Trim().Split(':');
            return parts.Length > 1 ? parts[1].Trim() : "default";
        }
        catch
        {
            return "default";
        }
    }

    private static string RunCommand(string command, string args)
    {
        var psi = new ProcessStartInfo(command, args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
            CreateNoWindow         = true
        };

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start {command}.");

        process.WaitForExit();
        return process.StandardOutput.ReadToEnd();
    }

    private static async Task<string> RunCommandAsync(string command, string args, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(command, args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
            CreateNoWindow         = true
        };

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start {command}.");

        var output = await process.StandardOutput.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync(ct);
            throw new InvalidOperationException($"lp exited with code {process.ExitCode}: {error}");
        }

        return output;
    }

    private void TryDeleteFile(string path)
    {
        try { File.Delete(path); }
        catch (Exception ex) { _logger.LogWarning(ex, "Could not delete temp print file: {Path}", path); }
    }
}

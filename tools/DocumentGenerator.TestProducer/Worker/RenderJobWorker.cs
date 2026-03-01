using System.Text.Json;
using DocumentGenerator.Core.Models;
using DocumentGenerator.Messaging.Messages;
using DocumentGenerator.TestProducer.Configuration;
using DocumentGenerator.TestProducer.Messaging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Rebus.Bus;

namespace DocumentGenerator.TestProducer.Worker;

/// <summary>
/// Headless <see cref="BackgroundService"/> that replaces the old interactive menu.
///
/// On each tick (controlled by <see cref="ProducerOptions.ScheduleInterval"/>) the worker:
///   1. Selects <see cref="ProducerOptions.BatchSize"/> template(s) from the configured
///      templates directory (round-robin).
///   2. Inlines any external HTML/CSS files so the Kafka payload is self-contained.
///   3. Publishes a <see cref="DocumentRenderRequest"/> per template.
///   4. Awaits the <see cref="DocumentRenderResult"/> reply (up to
///      <see cref="ProducerOptions.ResultTimeoutSeconds"/>).
///   5. Saves inline PDFs to <see cref="ProducerOptions.OutputDirectory"/>.
///
/// The worker is resilient: a failed batch is logged and the service continues.
/// </summary>
public sealed class RenderJobWorker : BackgroundService
{
    private readonly IBus            _bus;
    private readonly ResultStore     _resultStore;
    private readonly ProducerOptions _options;
    private readonly ILogger<RenderJobWorker> _logger;

    private static readonly JsonSerializerOptions JsonOpts =
        new() { PropertyNameCaseInsensitive = true };

    // Template files resolved once at startup — cycled round-robin.
    private IReadOnlyList<string> _templateFiles = [];
    private int _templateIndex;

    public RenderJobWorker(
        IBus bus,
        ResultStore resultStore,
        IOptions<ProducerOptions> options,
        ILogger<RenderJobWorker> logger)
    {
        _bus         = bus;
        _resultStore = resultStore;
        _options     = options.Value;
        _logger      = logger;
    }

    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "RenderJobWorker starting — Interval: {Interval}, BatchSize: {BatchSize}, " +
            "ReturnInline: {ReturnInline}, OutputDir: {OutputDir}",
            _options.ScheduleInterval,
            _options.BatchSize,
            _options.ReturnPdfInline,
            _options.OutputDirectory);

        _templateFiles = ResolveTemplateFiles();

        if (_templateFiles.Count == 0)
        {
            _logger.LogWarning("No template files found. Worker will idle until templates appear.");
        }
        else
        {
            _logger.LogInformation(
                "Loaded {Count} template(s): {Files}",
                _templateFiles.Count,
                string.Join(", ", _templateFiles.Select(Path.GetFileName)));
        }

        await base.StartAsync(cancellationToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("RenderJobWorker stopping — cancelling {Pending} pending result(s).",
            _resultStore.PendingCount);

        _resultStore.CancelAll();
        await base.StopAsync(cancellationToken);
    }

    // -------------------------------------------------------------------------
    // Main loop
    // -------------------------------------------------------------------------

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("RenderJobWorker executing.");

        while (!stoppingToken.IsCancellationRequested)
        {
            // Re-discover templates each cycle so new files are picked up without restart.
            _templateFiles = ResolveTemplateFiles();

            if (_templateFiles.Count == 0)
            {
                _logger.LogWarning(
                    "No templates available — sleeping {Interval} before retrying.",
                    _options.ScheduleInterval);
            }
            else
            {
                await RunBatchAsync(stoppingToken);
            }

            try
            {
                await Task.Delay(_options.ScheduleInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Host is shutting down — exit cleanly.
                break;
            }
        }

        _logger.LogInformation("RenderJobWorker loop exited.");
    }

    // -------------------------------------------------------------------------
    // Batch processing
    // -------------------------------------------------------------------------

    private async Task RunBatchAsync(CancellationToken stoppingToken)
    {
        var batchId = Guid.NewGuid();

        _logger.LogInformation(
            "Starting batch — BatchId: {BatchId}, Size: {BatchSize}, TemplateCount: {TemplateCount}",
            batchId, _options.BatchSize, _templateFiles.Count);

        var tasks = new List<Task>(_options.BatchSize);

        for (var i = 0; i < _options.BatchSize; i++)
        {
            if (stoppingToken.IsCancellationRequested) break;

            var templateFile = PickNextTemplate();
            tasks.Add(SendAndAwaitAsync(batchId, templateFile, stoppingToken));
        }

        try
        {
            await Task.WhenAll(tasks);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Individual job errors are logged inside SendAndAwaitAsync;
            // WhenAll re-throws the first — log it at batch level too.
            _logger.LogError(ex,
                "Batch {BatchId} completed with one or more errors.", batchId);
        }
    }

    private async Task SendAndAwaitAsync(Guid batchId, string templateFile, CancellationToken stoppingToken)
    {
        var correlationId = Guid.NewGuid();
        string? documentType = null;

        try
        {
            _logger.LogDebug(
                "Loading template — BatchId: {BatchId}, CorrelationId: {CorrelationId}, File: {File}",
                batchId, correlationId, Path.GetFileName(templateFile));

            var template = await LoadTemplateAsync(templateFile);
            documentType = template.DocumentType;

            var request = new DocumentRenderRequest
            {
                CorrelationId   = correlationId,
                DeviceId        = $"producer-{Environment.MachineName}",
                SessionId       = batchId.ToString("N"),
                Template        = template,
                RequestedAt     = DateTimeOffset.UtcNow,
                ReturnPdfInline = _options.ReturnPdfInline
            };

            var tcs = new TaskCompletionSource<DocumentRenderResult>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            _resultStore.Register(correlationId, tcs);

            _logger.LogInformation(
                "Publishing render request — CorrelationId: {CorrelationId}, " +
                "DocumentType: {DocumentType}, BatchId: {BatchId}, ReturnInline: {ReturnInline}",
                correlationId, documentType, batchId, _options.ReturnPdfInline);

            await _bus.Send(request);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            cts.CancelAfter(TimeSpan.FromSeconds(_options.ResultTimeoutSeconds));
            cts.Token.Register(() => tcs.TrySetCanceled(cts.Token));

            var result = await tcs.Task;
            await HandleResultAsync(result, documentType, correlationId, batchId);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                "Job cancelled by shutdown — CorrelationId: {CorrelationId}, BatchId: {BatchId}",
                correlationId, batchId);
        }
        catch (OperationCanceledException)
        {
            _logger.LogError(
                "Job timed out after {Timeout}s — CorrelationId: {CorrelationId}, " +
                "DocumentType: {DocumentType}, BatchId: {BatchId}",
                _options.ResultTimeoutSeconds, correlationId, documentType ?? "unknown", batchId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Job failed — CorrelationId: {CorrelationId}, DocumentType: {DocumentType}, " +
                "BatchId: {BatchId}",
                correlationId, documentType ?? "unknown", batchId);
        }
    }

    private async Task HandleResultAsync(
        DocumentRenderResult result,
        string documentType,
        Guid correlationId,
        Guid batchId)
    {
        if (!result.Success)
        {
            _logger.LogError(
                "Render failed — CorrelationId: {CorrelationId}, DocumentType: {DocumentType}, " +
                "BatchId: {BatchId}, Error: {Error}",
                correlationId, documentType, batchId, result.ErrorMessage);
            return;
        }

        if (result.PdfBase64 is not null)
        {
            var pdfBytes  = Convert.FromBase64String(result.PdfBase64);
            var outputDir = ResolveOutputDir();
            Directory.CreateDirectory(outputDir);

            var fileName   = $"{documentType}_{correlationId:N}.pdf";
            var outputPath = Path.Combine(outputDir, fileName);
            await File.WriteAllBytesAsync(outputPath, pdfBytes);

            _logger.LogInformation(
                "PDF saved (inline) — CorrelationId: {CorrelationId}, DocumentType: {DocumentType}, " +
                "BatchId: {BatchId}, Bytes: {Bytes}, ElapsedMs: {ElapsedMs}, Path: {Path}",
                correlationId, documentType, batchId,
                pdfBytes.Length, (int)result.ElapsedTime.TotalMilliseconds, outputPath);
        }
        else if (result.PdfPath is not null)
        {
            _logger.LogInformation(
                "PDF written by server (path mode) — CorrelationId: {CorrelationId}, " +
                "DocumentType: {DocumentType}, BatchId: {BatchId}, ElapsedMs: {ElapsedMs}, " +
                "ServerPath: {ServerPath}",
                correlationId, documentType, batchId,
                (int)result.ElapsedTime.TotalMilliseconds, result.PdfPath);
        }
        else
        {
            _logger.LogWarning(
                "Render succeeded but no PDF payload — CorrelationId: {CorrelationId}, BatchId: {BatchId}",
                correlationId, batchId);
        }
    }

    // -------------------------------------------------------------------------
    // Template loading helpers
    // -------------------------------------------------------------------------

    private async Task<DocumentTemplate> LoadTemplateAsync(string jsonPath)
    {
        var json     = await File.ReadAllTextAsync(jsonPath);
        var template = JsonSerializer.Deserialize<DocumentTemplate>(json, JsonOpts)
                       ?? throw new InvalidOperationException($"Failed to deserialise template: {jsonPath}");

        // Inline external HTML / CSS so the Kafka payload is self-contained.
        if (string.IsNullOrWhiteSpace(template.Template.HtmlPath))
            return template;

        var baseDir  = Path.GetDirectoryName(jsonPath)!;
        var htmlPath = Resolve(template.Template.HtmlPath, baseDir);
        var cssPath  = string.IsNullOrWhiteSpace(template.Template.CssPath)
                           ? null
                           : Resolve(template.Template.CssPath, baseDir);

        return new DocumentTemplate
        {
            DocumentType = template.DocumentType,
            Version      = template.Version,
            Branding     = template.Branding,
            Variables    = template.Variables,
            Pdf          = template.Pdf,
            Template = new TemplateContent
            {
                Html     = await File.ReadAllTextAsync(htmlPath),
                Css      = cssPath is null ? null : await File.ReadAllTextAsync(cssPath),
                Partials = template.Template.Partials
            }
        };

        static string Resolve(string path, string baseDir) =>
            Path.IsPathRooted(path) ? path : Path.Combine(baseDir, path);
    }

    private IReadOnlyList<string> ResolveTemplateFiles()
    {
        var dir = ResolveTemplatesDir();
        if (dir is null) return [];

        // If explicit template list configured, use that.
        if (_options.Templates is { Count: > 0 })
        {
            return _options.Templates
                .Select(f => Path.IsPathRooted(f) ? f : Path.Combine(dir, f))
                .Where(File.Exists)
                .ToList();
        }

        // Auto-discover all sample JSON files.
        return Directory.GetFiles(dir, "sample-*.json", SearchOption.TopDirectoryOnly);
    }

    private string? ResolveTemplatesDir()
    {
        // Explicit path in config?
        if (!string.IsNullOrWhiteSpace(_options.TemplatesDirectory))
        {
            var configured = Path.IsPathRooted(_options.TemplatesDirectory)
                ? _options.TemplatesDirectory
                : Path.GetFullPath(_options.TemplatesDirectory);

            if (Directory.Exists(configured)) return configured;

            _logger.LogWarning(
                "Configured TemplatesDirectory not found: {Dir}", configured);
            return null;
        }

        // Walk up from the executable to find a 'templates' directory.
        var dir = Path.GetDirectoryName(
            System.Reflection.Assembly.GetExecutingAssembly().Location)!;

        for (var i = 0; i < 10; i++)
        {
            var candidate = Path.Combine(dir, "templates");
            if (Directory.Exists(candidate)) return candidate;

            var parent = Directory.GetParent(dir);
            if (parent is null) break;
            dir = parent.FullName;
        }

        _logger.LogWarning("Could not locate a 'templates' directory by walking up from the executable.");
        return null;
    }

    private string ResolveOutputDir()
    {
        return Path.IsPathRooted(_options.OutputDirectory)
            ? _options.OutputDirectory
            : Path.GetFullPath(_options.OutputDirectory);
    }

    private string PickNextTemplate()
    {
        var file = _templateFiles[_templateIndex % _templateFiles.Count];
        _templateIndex++;
        return file;
    }
}

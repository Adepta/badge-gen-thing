using System.Text.Json;
using DocumentGenerator.Core.Interfaces;
using DocumentGenerator.Core.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocumentGenerator.Console;

/// <summary>
/// Hosted worker that scans the configured templates directory, renders each
/// template to PDF, and writes the output to the configured output directory.
///
/// In future this same worker pattern is trivially replaced/supplemented by:
///   - An ASP.NET Core controller that accepts HTTP POST payloads
///   - A RabbitMQ IQueueConsumer implementation
/// </summary>
public sealed class DocumentGeneratorWorker : BackgroundService
{
    private readonly IDocumentPipeline _pipeline;
    private readonly ILogger<DocumentGeneratorWorker> _logger;
    private readonly DocumentGeneratorOptions _options;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    public DocumentGeneratorWorker(
        IDocumentPipeline pipeline,
        IOptions<DocumentGeneratorOptions> options,
        ILogger<DocumentGeneratorWorker> logger)
    {
        _pipeline = pipeline;
        _options  = options.Value;
        _logger   = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var templatesDir = Path.GetFullPath(_options.TemplatesPath);
        var outputDir    = Path.GetFullPath(_options.OutputPath);

        if (!Directory.Exists(templatesDir))
        {
            _logger.LogWarning("Templates directory not found: {Path}. Creating it.", templatesDir);
            Directory.CreateDirectory(templatesDir);
        }

        Directory.CreateDirectory(outputDir);

        var templateFiles = Directory.GetFiles(templatesDir, "*.json", SearchOption.AllDirectories);

        if (templateFiles.Length == 0)
        {
            _logger.LogWarning("No template files found in {TemplatesPath}", templatesDir);
            return;
        }

        _logger.LogInformation("Found {Count} template file(s) — rendering in parallel", templateFiles.Length);

        // Render all templates concurrently — the pool manages Chromium concurrency
        var tasks = templateFiles.Select(file => RenderFileAsync(file, outputDir, stoppingToken));
        var results = await Task.WhenAll(tasks);

        var succeeded = results.Count(r => r);
        var failed    = results.Length - succeeded;

        _logger.LogInformation(
            "Rendering complete — {Succeeded} succeeded, {Failed} failed",
            succeeded, failed);
    }

    private async Task<bool> RenderFileAsync(string templatePath, string outputDir, CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("Processing template: {File}", Path.GetFileName(templatePath));

            await using var stream = File.OpenRead(templatePath);
            var template = await JsonSerializer.DeserializeAsync<DocumentTemplate>(stream, JsonOptions, ct)
                ?? throw new InvalidOperationException($"Failed to deserialise template: {templatePath}");

            var request = new RenderRequest { Template = template };
            var result  = await _pipeline.ExecuteAsync(request, ct);

            var outputFile = Path.Combine(
                outputDir,
                $"{template.DocumentType}_{request.JobId:N}.pdf");

            await File.WriteAllBytesAsync(outputFile, result.PdfBytes, ct);

            _logger.LogInformation(
                "Wrote {Bytes:N0} bytes → {OutputFile} ({Elapsed}ms)",
                result.PdfBytes.Length,
                outputFile,
                (int)result.ElapsedTime.TotalMilliseconds);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to render template: {File}", Path.GetFileName(templatePath));
            return false;
        }
    }
}

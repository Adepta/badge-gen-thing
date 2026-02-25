using System.Text.Json;
using DocumentGenerator.Core.Interfaces;
using DocumentGenerator.Core.Models;
using DocumentGenerator.Pdf;
using DocumentGenerator.Templating;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace DocumentGenerator.IntegrationTests.Worker;

/// <summary>
/// Integration tests that replicate the file-mode worker's read-deserialise-render-write
/// loop against real temp directories.
///
/// We avoid a direct reference to <c>DocumentGenerator.Console</c> (which is an Exe)
/// by exercising the same behaviour through the public API:
///   1. Serialise a <see cref="DocumentTemplate"/> to a temp JSON file.
///   2. Deserialise it (as the worker does).
///   3. Run it through the real pipeline (real template engine, mock renderer).
///   4. Assert the output file was produced with the expected PDF content.
/// </summary>
public sealed class FileModeWorkerIntegrationTests : IDisposable
{
    private readonly string _templatesDir;
    private readonly string _outputDir;
    private static readonly byte[] FakePdfBytes = [0x25, 0x50, 0x44, 0x46]; // %PDF

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling         = JsonCommentHandling.Skip,
        WriteIndented               = true
    };

    public FileModeWorkerIntegrationTests()
    {
        var root      = Path.Combine(Path.GetTempPath(), $"docgen_test_{Guid.NewGuid():N}");
        _templatesDir = Path.Combine(root, "templates");
        _outputDir    = Path.Combine(root, "output");

        Directory.CreateDirectory(_templatesDir);
        Directory.CreateDirectory(_outputDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(Path.GetDirectoryName(_templatesDir)!, true); }
        catch { /* best-effort cleanup */ }
    }

    // -----------------------------------------------------------------------
    // Serialisation round-trip
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Template_SerialiseAndDeserialise_RoundTripsCorrectly()
    {
        var original = BuildTemplate("badge", "<p>{{variables.name}}</p>");

        var json       = JsonSerializer.Serialize(original, JsonOptions);
        var restored   = JsonSerializer.Deserialize<DocumentTemplate>(json, JsonOptions);

        restored.Should().NotBeNull();
        restored!.DocumentType.Should().Be("badge");
        restored.Template.Html.Should().Be("<p>{{variables.name}}</p>");
    }

    [Fact]
    public async Task Template_DeserialiseFromFile_ProducesCorrectTemplate()
    {
        var template = BuildTemplate("invoice", "<h1>Invoice</h1>");
        await WriteTemplateFileAsync("invoice.json", template);

        var files = Directory.GetFiles(_templatesDir, "*.json");
        files.Should().HaveCount(1);

        await using var stream   = File.OpenRead(files[0]);
        var deserialized = await JsonSerializer.DeserializeAsync<DocumentTemplate>(stream, JsonOptions);

        deserialized!.DocumentType.Should().Be("invoice");
        deserialized.Template.Html.Should().Be("<h1>Invoice</h1>");
    }

    // -----------------------------------------------------------------------
    // Full file → render → output loop
    // -----------------------------------------------------------------------

    [Fact]
    public async Task WorkerLoop_SingleTemplate_WritesPdfToOutputDirectory()
    {
        var template = BuildTemplate("badge", "<p>{{variables.name}}</p>",
            variables: new() { ["name"] = "Alice" });

        await WriteTemplateFileAsync("badge.json", template);

        var files = Directory.GetFiles(_templatesDir, "*.json");
        await RenderFilesAsync(files, _outputDir);

        var outputFiles = Directory.GetFiles(_outputDir, "*.pdf");
        outputFiles.Should().HaveCount(1);
    }

    [Fact]
    public async Task WorkerLoop_SingleTemplate_PdfBytesMatchRenderer()
    {
        var template = BuildTemplate("badge", "<p>test</p>");
        await WriteTemplateFileAsync("badge.json", template);

        var files = Directory.GetFiles(_templatesDir, "*.json");
        await RenderFilesAsync(files, _outputDir);

        var outputFile = Directory.GetFiles(_outputDir, "*.pdf").Single();
        var bytes      = await File.ReadAllBytesAsync(outputFile);
        bytes.Should().Equal(FakePdfBytes);
    }

    [Fact]
    public async Task WorkerLoop_MultipleTemplates_WritesOnePdfPerTemplate()
    {
        await WriteTemplateFileAsync("badge.json",   BuildTemplate("badge",   "<p>badge</p>"));
        await WriteTemplateFileAsync("invoice.json", BuildTemplate("invoice", "<p>invoice</p>"));

        var files = Directory.GetFiles(_templatesDir, "*.json");
        await RenderFilesAsync(files, _outputDir);

        var outputFiles = Directory.GetFiles(_outputDir, "*.pdf");
        outputFiles.Should().HaveCount(2);
    }

    [Fact]
    public async Task WorkerLoop_OutputFilename_ContainsDocumentType()
    {
        var template = BuildTemplate("certificate", "<p>cert</p>");
        await WriteTemplateFileAsync("cert.json", template);

        var files = Directory.GetFiles(_templatesDir, "*.json");
        await RenderFilesAsync(files, _outputDir);

        var outputFile = Directory.GetFiles(_outputDir, "*.pdf").Single();
        Path.GetFileName(outputFile).Should().StartWith("certificate_");
    }

    [Fact]
    public async Task WorkerLoop_EmptyTemplateDirectory_ProducesNoPdfs()
    {
        // No files written — templates dir is empty
        var files = Directory.GetFiles(_templatesDir, "*.json");

        await RenderFilesAsync(files, _outputDir);

        Directory.GetFiles(_outputDir, "*.pdf").Should().BeEmpty();
    }

    [Fact]
    public async Task WorkerLoop_VariablesInTemplate_AreRenderedIntoHtml()
    {
        var rendererMock = new Mock<IDocumentRenderer>();
        string? capturedHtml = null;

        rendererMock
            .Setup(r => r.RenderPdfAsync(It.IsAny<string>(), It.IsAny<PdfOptions>(), It.IsAny<CancellationToken>()))
            .Callback<string, PdfOptions, CancellationToken>((html, _, _) => capturedHtml = html)
            .ReturnsAsync(FakePdfBytes);

        var template = BuildTemplate("badge", "{{variables.attendee}}",
            variables: new() { ["attendee"] = "Bob Smith" });

        await WriteTemplateFileAsync("badge.json", template);

        var files = Directory.GetFiles(_templatesDir, "*.json");
        await RenderFilesAsync(files, _outputDir, rendererMock);

        capturedHtml.Should().Be("Bob Smith");
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private IDocumentPipeline BuildPipeline(Mock<IDocumentRenderer>? rendererMock = null)
    {
        if (rendererMock is null)
        {
            rendererMock = new Mock<IDocumentRenderer>();
            rendererMock
                .Setup(r => r.RenderPdfAsync(It.IsAny<string>(), It.IsAny<PdfOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(FakePdfBytes);
        }

        ITemplateEngine engine = new HandlebarsTemplateEngine(
            NullLogger<HandlebarsTemplateEngine>.Instance);

        return new DocumentPipeline(
            engine,
            rendererMock.Object,
            NullLogger<DocumentPipeline>.Instance);
    }

    private async Task RenderFilesAsync(
        string[] files,
        string outputDir,
        Mock<IDocumentRenderer>? rendererMock = null)
    {
        var pipeline = BuildPipeline(rendererMock);

        var tasks = files.Select(async file =>
        {
            await using var stream   = File.OpenRead(file);
            var template = await JsonSerializer.DeserializeAsync<DocumentTemplate>(stream, JsonOptions)
                ?? throw new InvalidOperationException($"Failed to deserialise: {file}");

            var request = new RenderRequest { Template = template };
            var result  = await pipeline.ExecuteAsync(request);

            var outputFile = Path.Combine(
                outputDir,
                $"{template.DocumentType}_{request.JobId:N}.pdf");

            await File.WriteAllBytesAsync(outputFile, result.PdfBytes);
        });

        await Task.WhenAll(tasks);
    }

    private async Task WriteTemplateFileAsync(string fileName, DocumentTemplate template)
    {
        var json = JsonSerializer.Serialize(template, JsonOptions);
        await File.WriteAllTextAsync(Path.Combine(_templatesDir, fileName), json);
    }

    private static DocumentTemplate BuildTemplate(
        string documentType,
        string html,
        Dictionary<string, object?>? variables = null) =>
        new()
        {
            DocumentType = documentType,
            Version      = "1.0",
            Template     = new TemplateContent { Html = html },
            Variables    = variables ?? []
        };
}

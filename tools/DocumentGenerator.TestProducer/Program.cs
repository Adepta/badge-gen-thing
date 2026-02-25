using DocumentGenerator.Core.Models;
using DocumentGenerator.Messaging.Messages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Rebus.Bus;
using Rebus.Config;
using Rebus.Handlers;
using Rebus.Kafka;
using Rebus.Routing.TypeBased;
using Rebus.ServiceProvider;
using Spectre.Console;

const string brokers      = "localhost:9092";
const string requestTopic = "render.requests";
const string resultTopic  = "render.results";

// ---------------------------------------------------------------------------
// Header
// ---------------------------------------------------------------------------
AnsiConsole.Write(
    new FigletText("DocGenerator")
        .Centered()
        .Color(Color.Purple));

AnsiConsole.Write(new Rule("[grey]Test Producer[/]").RuleStyle("grey").Centered());
AnsiConsole.WriteLine();

var grid = new Grid().AddColumn().AddColumn();
grid.AddRow("[grey]Broker[/]",  $"[white]{brokers}[/]");
grid.AddRow("[grey]Request[/]", $"[white]{requestTopic}[/]");
grid.AddRow("[grey]Result[/]",  $"[white]{resultTopic}[/]");
AnsiConsole.Write(new Panel(grid).Header("[purple]Kafka[/]").BorderColor(Color.Grey));
AnsiConsole.WriteLine();

// ---------------------------------------------------------------------------
// Build Rebus host (once — reused across all renders in the session)
// ---------------------------------------------------------------------------
var sessionId = Guid.NewGuid();
var resultTcs = new ResultStore();

var host = Host.CreateDefaultBuilder()
    .ConfigureLogging(logging =>
    {
        logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.None);
    })
    .ConfigureServices(services =>
    {
        services.AddSingleton(resultTcs);
        services.AddRebusHandler<ResultHandler>();

        services.AddRebus(
            configure => configure
                .Transport(t => t.UseKafka(
                    brokers,
                    $"test-producer-{sessionId:N}"))
                .Routing(r => r.TypeBased()
                    .Map<DocumentRenderRequest>(requestTopic)),
            onCreated: _ => Task.CompletedTask
        );
    })
    .Build();

await AnsiConsole.Status()
    .Spinner(Spinner.Known.Dots)
    .SpinnerStyle(Style.Parse("purple"))
    .StartAsync("Connecting to Kafka...", async ctx =>
    {
        await host.StartAsync();
        ctx.Status("Connected");
        await Task.Delay(400);
    });

var bus = host.Services.GetRequiredService<IBus>();

// ---------------------------------------------------------------------------
// Locate the templates directory
// (walks up from the assembly dir, so works from dotnet run and published)
// ---------------------------------------------------------------------------
static string FindTemplatesDir()
{
    var dir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)!;
    for (var i = 0; i < 8; i++)
    {
        var candidate = Path.Combine(dir, "templates");
        if (Directory.Exists(candidate)) return candidate;
        var parent = Directory.GetParent(dir);
        if (parent is null) break;
        dir = parent.FullName;
    }
    throw new DirectoryNotFoundException("Could not locate the 'templates' directory.");
}

var templatesDir = FindTemplatesDir();

// ---------------------------------------------------------------------------
// Available badge / document variants
// Each entry maps a menu label → (documentType, json file in templates/)
// ---------------------------------------------------------------------------
var variants = new Dictionary<string, (string DocumentType, string JsonFile)>
{
    ["Pulse     — A6  — TechConf 2026     (Speaker)"]   = ("badge",   "sample-badge-pulse-a6.json"),
    ["Pulse     — CC  — TechConf 2026     (Speaker)"]   = ("badge",   "sample-badge-pulse-cc.json"),
    ["Carbon    — A6  — GameJam '26       (Hacker)"]    = ("badge",   "sample-badge-carbon-a6.json"),
    ["Carbon    — CC  — GameJam '26       (Hacker)"]    = ("badge",   "sample-badge-carbon-cc.json"),
    ["Executive — A6  — Global Leaders    (Delegate)"]  = ("badge",   "sample-badge-executive-a6.json"),
    ["Executive — CC  — Global Leaders    (Delegate)"]  = ("badge",   "sample-badge-executive-cc.json"),
    ["Invoice   — A4"]                                  = ("invoice", "sample-invoice.json"),
};

// ---------------------------------------------------------------------------
// Main menu loop
// ---------------------------------------------------------------------------
var jsonOptions = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };

while (true)
{
    AnsiConsole.WriteLine();
    AnsiConsole.Write(new Rule("[purple]Select a document[/]").RuleStyle("grey"));

    var choices = variants.Keys.ToList();
    choices.Add("[red]Exit[/]");

    var selection = AnsiConsole.Prompt(
        new SelectionPrompt<string>()
            .PageSize(10)
            .HighlightStyle(Style.Parse("purple bold"))
            .AddChoices(choices));

    if (selection == "[red]Exit[/]")
        break;

    var (documentType, jsonFile) = variants[selection];

    // ── Load template JSON ──────────────────────────────────────────────────
    var jsonPath = Path.Combine(templatesDir, jsonFile);
    var jsonText = await File.ReadAllTextAsync(jsonPath);
    var template = System.Text.Json.JsonSerializer.Deserialize<DocumentTemplate>(jsonText, jsonOptions)!;

    // ── Inline any external HTML / CSS files ────────────────────────────────
    // The Kafka payload must be self-contained (no file paths on the server).
    if (!string.IsNullOrWhiteSpace(template.Template.HtmlPath))
    {
        var htmlFull = Resolve(template.Template.HtmlPath, templatesDir);
        var cssFull  = string.IsNullOrWhiteSpace(template.Template.CssPath)
                           ? null
                           : Resolve(template.Template.CssPath, templatesDir);

        template = new DocumentTemplate
        {
            DocumentType = template.DocumentType,
            Version      = template.Version,
            Branding     = template.Branding,
            Variables    = template.Variables,
            Pdf          = template.Pdf,
            Template     = new TemplateContent
            {
                Html     = await File.ReadAllTextAsync(htmlFull),
                Css      = cssFull is null ? null : await File.ReadAllTextAsync(cssFull),
                Partials = template.Template.Partials
            }
        };
    }

    static string Resolve(string path, string baseDir) =>
        Path.IsPathRooted(path) ? path : Path.Combine(baseDir, path);

    // ── Build and send the Kafka message ────────────────────────────────────
    var correlationId = Guid.NewGuid();
    var deviceId      = $"test-ipad-{Environment.MachineName}";

    var request = new DocumentRenderRequest
    {
        CorrelationId = correlationId,
        DeviceId      = deviceId,
        SessionId     = sessionId.ToString(),
        Template      = template,
        RequestedAt   = DateTimeOffset.UtcNow
    };

    var tcs = new TaskCompletionSource<DocumentRenderResult>();
    resultTcs.Register(correlationId, tcs);

    DocumentRenderResult? result = null;

    await AnsiConsole.Progress()
        .AutoClear(false)
        .Columns(
            new TaskDescriptionColumn { Alignment = Justify.Left },
            new ProgressBarColumn().FinishedStyle(Style.Parse("purple")),
            new SpinnerColumn(Spinner.Known.Dots).Style(Style.Parse("purple")))
        .StartAsync(async ctx =>
        {
            var sendSw   = System.Diagnostics.Stopwatch.StartNew();
            var renderSw = new System.Diagnostics.Stopwatch();

            var sendTask   = ctx.AddTask("[grey]Sending to Kafka...[/]",   maxValue: 1);
            var renderTask = ctx.AddTask("[grey]Waiting for render...[/]", maxValue: 1);

            await bus.Send(request);
            sendSw.Stop();
            sendTask.Increment(1);
            sendTask.Description = $"[green]Sent[/] [grey]({sendSw.ElapsedMilliseconds}ms)[/]";

            renderSw.Start();
            renderTask.IsIndeterminate = true;

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
            cts.Token.Register(() => tcs.TrySetCanceled());

            try
            {
                result = await tcs.Task;
                renderSw.Stop();
                renderTask.IsIndeterminate = false;
                renderTask.Increment(1);
                renderTask.Description = result.Success
                    ? $"[green]Render complete[/] [grey]({renderSw.ElapsedMilliseconds}ms)[/]"
                    : $"[red]Render failed[/] [grey]({renderSw.ElapsedMilliseconds}ms)[/]";
            }
            catch (OperationCanceledException)
            {
                renderSw.Stop();
                renderTask.IsIndeterminate = false;
                renderTask.Description = $"[red]Timed out[/] [grey]({renderSw.ElapsedMilliseconds}ms)[/]";
            }
        });

    AnsiConsole.WriteLine();

    if (result is null)
    {
        AnsiConsole.MarkupLine("[red]No result received within 120s.[/] Check [link]http://localhost:8080[/] for dead-letter messages.");
        continue;
    }

    var resultGrid = new Grid().AddColumn(new GridColumn().NoWrap()).AddColumn();
    resultGrid.AddRow("[grey]Correlation ID[/]", $"[white]{result.CorrelationId}[/]");
    resultGrid.AddRow("[grey]Device[/]",         $"[white]{result.DeviceId}[/]");
    resultGrid.AddRow("[grey]Type[/]",           $"[white]{result.DocumentType}[/]");
    resultGrid.AddRow("[grey]Elapsed[/]",        $"[white]{result.ElapsedTime.TotalMilliseconds:N0}ms[/]");

    if (result.Success && result.PdfBase64 is not null)
    {
        var pdfBytes  = Convert.FromBase64String(result.PdfBase64);
        var outputDir = Path.GetFullPath("Generated");
        Directory.CreateDirectory(outputDir);
        var outputPath = Path.Combine(outputDir, $"{documentType}_{result.DocumentType}_{correlationId:N}.pdf");
        await File.WriteAllBytesAsync(outputPath, pdfBytes);

        resultGrid.AddRow("[grey]PDF size[/]",  $"[white]{pdfBytes.Length:N0} bytes[/]");
        resultGrid.AddRow("[grey]Saved to[/]",  $"[green]{outputPath}[/]");

        AnsiConsole.Write(
            new Panel(resultGrid)
                .Header("[green] Success [/]")
                .BorderColor(Color.Green));
    }
    else
    {
        resultGrid.AddRow("[grey]Error[/]", $"[red]{result.ErrorMessage}[/]");
        AnsiConsole.Write(
            new Panel(resultGrid)
                .Header("[red] Failed [/]")
                .BorderColor(Color.Red));
    }
}

await AnsiConsole.Status()
    .Spinner(Spinner.Known.Dots)
    .SpinnerStyle(Style.Parse("grey"))
    .StartAsync("Shutting down...", async ctx =>
    {
        await host.StopAsync();
    });

AnsiConsole.MarkupLine("[grey]Goodbye.[/]");

// =============================================================================
// Result store — maps CorrelationId → TCS so concurrent renders work
// =============================================================================
class ResultStore
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, TaskCompletionSource<DocumentRenderResult>> _pending = new();

    public void Register(Guid id, TaskCompletionSource<DocumentRenderResult> tcs) =>
        _pending[id] = tcs;

    public void Complete(DocumentRenderResult result)
    {
        if (_pending.TryRemove(result.CorrelationId, out var tcs))
            tcs.TrySetResult(result);
    }
}

// =============================================================================
// Rebus result handler
// =============================================================================
class ResultHandler(ResultStore store) : IHandleMessages<DocumentRenderResult>
{
    /// <inheritdoc/>
    public Task Handle(DocumentRenderResult message)
    {
        store.Complete(message);
        return Task.CompletedTask;
    }
}

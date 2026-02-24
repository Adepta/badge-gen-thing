using DocumentGenerator.Console;
using DocumentGenerator.Console.Logging;
using DocumentGenerator.Core.Configuration;
using DocumentGenerator.Core.Interfaces;
using DocumentGenerator.Messaging.Configuration;
using DocumentGenerator.Messaging.Extensions;
using DocumentGenerator.Pdf.Extensions;
using DocumentGenerator.Templating.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PuppeteerSharp;
using Spectre.Console;

var logBuffer   = new LogBuffer(capacity: 500);
var renderStats = new RenderStats();

AnsiConsole.Write(new FigletText("DocGenerator").Centered().Color(Color.Purple));
AnsiConsole.Write(new Rule("[grey]Document Render Service[/]").RuleStyle("grey").Centered());
AnsiConsole.WriteLine();

await AnsiConsole.Progress()
    .AutoClear(false)
    .Columns(
        new TaskDescriptionColumn(),
        new ProgressBarColumn().FinishedStyle(Style.Parse("purple")),
        new SpinnerColumn(Spinner.Known.Dots).Style(Style.Parse("purple")))
    .StartAsync(async ctx =>
    {
        var task = ctx.AddTask("[grey]Checking Chromium...[/]", maxValue: 1);
        task.IsIndeterminate = true;
        await new BrowserFetcher().DownloadAsync();
        task.IsIndeterminate = false;
        task.Increment(1);
        task.Description = "[green]Chromium ready[/]";
    });

// dotnet run sets CWD to the solution root; pin content root to the assembly
// directory so appsettings.json and templates/ are always resolved correctly.
var projectDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)!;

var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args            = args,
    ContentRootPath = projectDir
});

builder.Services.AddSingleton(logBuffer);
builder.Services.AddSingleton(renderStats);
builder.Services.AddSingleton<IRenderMetrics>(renderStats);

builder.Logging.AddSpectreConsole(logBuffer, LogLevel.Debug);

var kafkaEnabled = builder.Configuration.GetValue<bool>("Kafka:Enabled", defaultValue: false);

builder.Services
    .Configure<BrowserPoolOptions>(builder.Configuration.GetSection(BrowserPoolOptions.SectionName))
    .Configure<DocumentGeneratorOptions>(builder.Configuration.GetSection(DocumentGeneratorOptions.SectionName))
    .AddTemplating()
    .AddPdfRendering();

builder.Services.AddHostedService<TuiRenderer>();

if (kafkaEnabled)
{
    var kafkaOptions = builder.Configuration
        .GetSection(KafkaOptions.SectionName)
        .Get<KafkaOptions>() ?? new KafkaOptions();

    builder.Services
        .Configure<KafkaOptions>(builder.Configuration.GetSection(KafkaOptions.SectionName))
        .AddRebusKafkaMessaging(kafkaOptions);

    logBuffer.Add(new LogBuffer.LogEntry(DateTime.Now, LogLevel.Information, "Startup",
        $"Kafka consumer — {kafkaOptions.BootstrapServers}, {kafkaOptions.RequestTopic} → {kafkaOptions.ResultTopic}"));
}
else
{
    builder.Services.AddHostedService<DocumentGeneratorWorker>();

    logBuffer.Add(new LogBuffer.LogEntry(DateTime.Now, LogLevel.Information, "Startup",
        "File worker — templates/ → output/"));
}

await builder.Build().RunAsync();

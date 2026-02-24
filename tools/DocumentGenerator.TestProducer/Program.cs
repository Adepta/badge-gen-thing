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
// Use a stable group ID for the session so we don't spam new Kafka topics
var sessionId  = Guid.NewGuid();
var resultTcs  = new ResultStore();

var host = Host.CreateDefaultBuilder()
    .ConfigureLogging(logging =>
    {
        // Suppress Rebus/Kafka noise — we show our own UI
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
// Main menu loop
// ---------------------------------------------------------------------------
var variants = new Dictionary<string, (string Label, Func<DocumentTemplate> Builder)>
{
    ["TechConf 2026 — A6 — Speaker"]          = ("badge", BuildTechConfBadge),
    ["DevSummit London — A6 — VIP"]           = ("badge", BuildDevSummitBadge),
    ["HealthTech Expo — A6 — Attendee"]       = ("badge", BuildHealthTechBadge),
    ["GameJam Weekend — Credit Card — Hacker"]= ("badge", BuildGameJamBadge),
    ["Corporate Summit — Credit Card — Delegate"] = ("badge", BuildCorporateBadge),
    ["Invoice — A4"]                          = ("invoice", BuildInvoiceTemplate),
};

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

    var (documentType, builder) = variants[selection];
    var template      = builder();
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

    // Register this TCS before sending so the handler can signal it
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

    // Result panel
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
        var outputPath = Path.Combine(outputDir, $"{documentType}_{correlationId:N}.pdf");
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
// A6 Badge — TechConf 2026 (purple + coral, Speaker)
// =============================================================================
static DocumentTemplate BuildTechConfBadge() => new()
{
    DocumentType = "badge",
    Version      = "1.0",
    Branding = new Branding
    {
        CompanyName     = "TechConf 2026",
        PrimaryColour   = "#6C3CE1",
        SecondaryColour = "#F3F0FF",
        BodyFont        = "Segoe UI, Arial, sans-serif",
        Custom          = new Dictionary<string, string> { ["accentColour"] = "#FF5A5F" }
    },
    Template  = new TemplateContent { Html = BadgeA6Html(), Css = BadgeA6Css() },
    Variables = new Dictionary<string, object?>
    {
        ["firstName"]  = "Jane",   ["lastName"]   = "Smith",
        ["jobTitle"]   = "Senior Engineer",        ["company"]    = "Acme Corp",
        ["ticketType"] = "Speaker", ["attendeeId"] = "TC2026-00842",
        ["sessionName"]= "Hall A — Keynote",
        ["eventDate"]  = "12–14 March 2026",       ["eventVenue"] = "ExCeL London"
    },
    Pdf = A6PdfOptions()
};

// =============================================================================
// A6 Badge — DevSummit London (dark navy + electric green, VIP)
// =============================================================================
static DocumentTemplate BuildDevSummitBadge() => new()
{
    DocumentType = "badge",
    Version      = "1.0",
    Branding = new Branding
    {
        CompanyName     = "DevSummit London",
        PrimaryColour   = "#0F172A",
        SecondaryColour = "#F0FDF4",
        BodyFont        = "Segoe UI, Arial, sans-serif",
        Custom          = new Dictionary<string, string> { ["accentColour"] = "#22C55E" }
    },
    Template  = new TemplateContent { Html = BadgeA6Html(), Css = BadgeA6Css() },
    Variables = new Dictionary<string, object?>
    {
        ["firstName"]  = "Marcus",  ["lastName"]   = "Webb",
        ["jobTitle"]   = "Staff Engineer",          ["company"]    = "Cloudbase Systems",
        ["ticketType"] = "VIP",     ["attendeeId"] = "DS2026-00017",
        ["sessionName"]= "Track B — Distributed Systems",
        ["eventDate"]  = "8–9 May 2026",            ["eventVenue"] = "The Barbican, London"
    },
    Pdf = A6PdfOptions()
};

// =============================================================================
// A6 Badge — HealthTech Expo (teal + amber, Attendee)
// =============================================================================
static DocumentTemplate BuildHealthTechBadge() => new()
{
    DocumentType = "badge",
    Version      = "1.0",
    Branding = new Branding
    {
        CompanyName     = "HealthTech Expo",
        PrimaryColour   = "#0D9488",
        SecondaryColour = "#FFFBEB",
        BodyFont        = "Segoe UI, Arial, sans-serif",
        Custom          = new Dictionary<string, string> { ["accentColour"] = "#F59E0B" }
    },
    Template  = new TemplateContent { Html = BadgeA6Html(), Css = BadgeA6Css() },
    Variables = new Dictionary<string, object?>
    {
        ["firstName"]  = "Priya",   ["lastName"]   = "Nair",
        ["jobTitle"]   = "Clinical Data Scientist", ["company"]    = "MedAnalytics Ltd",
        ["ticketType"] = "Attendee",["attendeeId"] = "HTE26-04421",
        ["sessionName"]= "Room 3 — AI Diagnostics",
        ["eventDate"]  = "22 June 2026",            ["eventVenue"] = "Manchester Central"
    },
    Pdf = A6PdfOptions()
};

// =============================================================================
// Credit Card Badge — GameJam Weekend (dark + neon lime)
// =============================================================================
static DocumentTemplate BuildGameJamBadge() => new()
{
    DocumentType = "badge",
    Version      = "1.0",
    Branding = new Branding
    {
        CompanyName     = "GameJam '26",
        PrimaryColour   = "#18181B",
        SecondaryColour = "#F4F4F5",
        BodyFont        = "Segoe UI, Arial, sans-serif",
        Custom          = new Dictionary<string, string> { ["accentColour"] = "#A3E635" }
    },
    Template  = new TemplateContent { Html = BadgeCreditCardHtml(), Css = BadgeCreditCardCss() },
    Variables = new Dictionary<string, object?>
    {
        ["firstName"]  = "Alex",    ["lastName"]   = "Chen",
        ["role"]       = "Hacker",  ["team"]       = "Team Voltage",
        ["attendeeId"] = "GJ26-0391",
        ["eventDate"]  = "14–16 Feb 2026",          ["eventVenue"] = "Tobacco Dock, London"
    },
    Pdf = CreditCardPdfOptions()
};

// =============================================================================
// Credit Card Badge — Corporate Summit (slate + gold)
// =============================================================================
static DocumentTemplate BuildCorporateBadge() => new()
{
    DocumentType = "badge",
    Version      = "1.0",
    Branding = new Branding
    {
        CompanyName     = "Global Leaders Summit",
        PrimaryColour   = "#334155",
        SecondaryColour = "#F8FAFC",
        BodyFont        = "Segoe UI, Arial, sans-serif",
        Custom          = new Dictionary<string, string> { ["accentColour"] = "#D4AF37" }
    },
    Template  = new TemplateContent { Html = BadgeCreditCardHtml(), Css = BadgeCreditCardCss() },
    Variables = new Dictionary<string, object?>
    {
        ["firstName"]  = "Sarah",   ["lastName"]   = "Okafor",
        ["role"]       = "Delegate",["team"]       = "Strategy & Innovation",
        ["attendeeId"] = "GLS26-1142",
        ["eventDate"]  = "3 Sept 2026",             ["eventVenue"] = "Canary Wharf, London"
    },
    Pdf = CreditCardPdfOptions()
};

// =============================================================================
// Invoice
// =============================================================================
static DocumentTemplate BuildInvoiceTemplate() => new()
{
    DocumentType = "invoice",
    Version      = "1.0",
    Branding = new Branding
    {
        CompanyName     = "Acme Corporation",
        LogoUrl         = "https://via.placeholder.com/150x50?text=ACME",
        PrimaryColour   = "#1A73E8",
        SecondaryColour = "#F8F9FA",
        HeadingFont     = "Arial, sans-serif",
        BodyFont        = "Georgia, serif",
        Custom          = new Dictionary<string, string> { ["tagline"] = "Quality you can trust" }
    },
    Template = new TemplateContent
    {
        Html = """
            <!DOCTYPE html><html><head><meta charset='utf-8'></head><body>
            <header>
              <div><img src='{{branding.logoUrl}}' /><div>{{branding.companyName}}</div></div>
              <div><h1>INVOICE</h1><p>#{{variables.invoiceNumber}}</p></div>
            </header>
            <section><h2>Bill To</h2><p>{{variables.customer.name}}</p></section>
            <table>
              <thead><tr><th>Description</th><th>Qty</th><th>Price</th><th>Total</th></tr></thead>
              <tbody>{{#each variables.lineItems}}<tr>
                <td>{{this.description}}</td><td>{{this.quantity}}</td>
                <td>{{currency this.unitPrice 'en-GB'}}</td><td>{{currency this.total 'en-GB'}}</td>
              </tr>{{/each}}</tbody>
              <tfoot><tr><td colspan='3'>Total</td><td>{{currency variables.totalAmount 'en-GB'}}</td></tr></tfoot>
            </table>
            </body></html>
            """,
        Css = """
            body{font-family:{{branding.bodyFont}};padding:20px}
            header{display:flex;justify-content:space-between;border-bottom:3px solid {{branding.primaryColour}};padding-bottom:20px;margin-bottom:30px}
            h1{color:{{branding.primaryColour}}}
            table{width:100%;border-collapse:collapse}
            th{background:{{branding.primaryColour}};color:#fff;padding:10px;text-align:left}
            td{padding:8px;border-bottom:1px solid #ddd}
            """
    },
    Variables = new Dictionary<string, object?>
    {
        ["invoiceNumber"] = "INV-2026-TEST",
        ["customer"]      = new Dictionary<string, object?> { ["name"] = "Jane Smith" },
        ["lineItems"]     = new List<object>
        {
            new Dictionary<string, object?> { ["description"] = "Consulting", ["quantity"] = 10, ["unitPrice"] = 150.0, ["total"] = 1500.0 }
        },
        ["totalAmount"] = 1500.0
    },
    Pdf = new PdfOptions { Format = "A4", PrintBackground = true }
};

// =============================================================================
// Shared HTML / CSS templates
// =============================================================================

static string BadgeA6Html() => """
    <!DOCTYPE html>
    <html>
    <head><meta charset='utf-8'></head>
    <body>
    <div class='badge'>
      <div class='header'>
        <div class='header-left'>
          <div class='event-name'>{{branding.companyName}}</div>
          <div class='event-date'>{{variables.eventDate}}</div>
          <div class='event-venue'>{{variables.eventVenue}}</div>
        </div>
        <div class='ticket-pill ticket-pill--{{lower variables.ticketType}}'>{{upper variables.ticketType}}</div>
      </div>
      <div class='stripe'></div>
      <div class='body'>
        <div class='name-block'>
          <div class='first-name'>{{upper variables.firstName}}</div>
          <div class='last-name'>{{upper variables.lastName}}</div>
        </div>
        <div class='meta-block'>
          <div class='job-title'>{{variables.jobTitle}}</div>
          <div class='company'>{{variables.company}}</div>
        </div>
      </div>
      <div class='footer'>
        <div class='footer-left'>
          <div class='footer-label'>Attendee ID</div>
          <div class='footer-value'>{{variables.attendeeId}}</div>
        </div>
        <div class='footer-right'>
          <div class='footer-label'>Session</div>
          <div class='footer-value'>{{variables.sessionName}}</div>
        </div>
      </div>
    </div>
    </body></html>
    """;

static string BadgeA6Css() => """
    @import url('https://fonts.googleapis.com/css2?family=Inter:wght@400;600;700;900&display=swap');
    * { margin: 0; padding: 0; box-sizing: border-box; }
    body { width: 105mm; height: 148mm; font-family: Inter, {{branding.bodyFont}}; background: #fff; overflow: hidden; }
    .badge { width: 105mm; height: 148mm; display: flex; flex-direction: column; overflow: hidden; }
    .header { background: {{branding.primaryColour}}; padding: 7mm 6mm 6mm; display: flex; justify-content: space-between; align-items: flex-start; flex-shrink: 0; }
    .event-name { font-size: 13pt; font-weight: 900; color: #fff; letter-spacing: -0.3px; line-height: 1.1; }
    .event-date { font-size: 7.5pt; color: rgba(255,255,255,0.85); margin-top: 1.5mm; font-weight: 600; }
    .event-venue { font-size: 7pt; color: rgba(255,255,255,0.65); margin-top: 0.5mm; }
    .ticket-pill { font-size: 6.5pt; font-weight: 700; letter-spacing: 0.8px; padding: 1.5mm 3mm; border-radius: 20mm; color: #fff; white-space: nowrap; align-self: flex-start; margin-top: 1mm; }
    .ticket-pill--speaker  { background: {{branding.custom.accentColour}}; }
    .ticket-pill--attendee { background: #22C55E; }
    .ticket-pill--vip      { background: #A855F7; }
    .ticket-pill--staff    { background: #3B82F6; }
    .ticket-pill--sponsor  { background: #F59E0B; color: #1a1a1a; }
    .stripe { height: 5mm; background: linear-gradient(90deg, {{branding.custom.accentColour}} 0%, {{branding.primaryColour}} 100%); flex-shrink: 0; }
    .body { flex: 1; padding: 8mm 6mm 4mm; display: flex; flex-direction: column; justify-content: center; gap: 4mm; }
    .first-name { font-size: 28pt; font-weight: 900; color: {{branding.primaryColour}}; line-height: 1; letter-spacing: -0.5px; }
    .last-name { font-size: 20pt; font-weight: 700; color: #1a1a1a; line-height: 1; letter-spacing: -0.3px; margin-top: 1mm; }
    .meta-block { border-left: 0.8mm solid {{branding.custom.accentColour}}; padding-left: 3mm; margin-top: 2mm; }
    .job-title { font-size: 9.5pt; font-weight: 600; color: #111; }
    .company { font-size: 8.5pt; color: #555; margin-top: 0.8mm; }
    .footer { background: {{branding.secondaryColour}}; border-top: 0.3mm solid rgba(0,0,0,0.08); padding: 3.5mm 6mm; display: flex; justify-content: space-between; align-items: flex-start; flex-shrink: 0; }
    .footer-label { font-size: 5.5pt; font-weight: 700; text-transform: uppercase; letter-spacing: 0.6px; color: {{branding.primaryColour}}; margin-bottom: 0.8mm; }
    .footer-value { font-size: 7.5pt; font-weight: 600; color: #222; }
    .footer-right { text-align: right; }
    """;

static string BadgeCreditCardHtml() => """
    <!DOCTYPE html>
    <html>
    <head><meta charset='utf-8'></head>
    <body>
    <div class='badge'>
      <div class='top-bar'></div>
      <div class='content'>
        <div class='left'>
          <div class='event-name'>{{branding.companyName}}</div>
          <div class='event-sub'>{{variables.eventDate}} · {{variables.eventVenue}}</div>
          <div class='name'>{{upper variables.firstName}} {{upper variables.lastName}}</div>
          <div class='role-row'>
            <span class='role-pill'>{{variables.role}}</span>
            <span class='team'>{{variables.team}}</span>
          </div>
        </div>
        <div class='right'>
          <div class='id-label'>ID</div>
          <div class='id-value'>{{variables.attendeeId}}</div>
        </div>
      </div>
      <div class='bottom-bar'></div>
    </div>
    </body></html>
    """;

static string BadgeCreditCardCss() => """
    @import url('https://fonts.googleapis.com/css2?family=Inter:wght@400;600;700;900&display=swap');
    * { margin: 0; padding: 0; box-sizing: border-box; }
    body { width: 85.6mm; height: 54mm; font-family: Inter, {{branding.bodyFont}}; overflow: hidden; }
    .badge { width: 85.6mm; height: 54mm; background: {{branding.primaryColour}}; display: flex; flex-direction: column; border-radius: 2.5mm; overflow: hidden; }
    .top-bar { height: 2.5mm; background: {{branding.custom.accentColour}}; flex-shrink: 0; }
    .content { flex: 1; padding: 3mm 4mm; display: flex; justify-content: space-between; align-items: stretch; }
    .left { display: flex; flex-direction: column; justify-content: space-between; flex: 1; }
    .event-name { font-size: 7pt; font-weight: 900; color: {{branding.custom.accentColour}}; letter-spacing: 0.5px; text-transform: uppercase; }
    .event-sub { font-size: 5pt; color: rgba(255,255,255,0.5); margin-top: 0.3mm; }
    .name { font-size: 13pt; font-weight: 900; color: #fff; letter-spacing: -0.3px; line-height: 1.05; margin-top: 2mm; }
    .role-row { display: flex; align-items: center; gap: 2mm; margin-top: 1.5mm; }
    .role-pill { font-size: 5.5pt; font-weight: 700; text-transform: uppercase; letter-spacing: 0.6px; padding: 0.6mm 2mm; border-radius: 10mm; background: {{branding.custom.accentColour}}; color: {{branding.primaryColour}}; }
    .team { font-size: 6pt; color: rgba(255,255,255,0.6); }
    .right { display: flex; flex-direction: column; justify-content: flex-end; align-items: flex-end; padding-left: 3mm; }
    .id-label { font-size: 5pt; font-weight: 700; text-transform: uppercase; letter-spacing: 0.8px; color: rgba(255,255,255,0.4); margin-bottom: 0.5mm; }
    .id-value { font-size: 6pt; font-weight: 700; color: rgba(255,255,255,0.75); letter-spacing: 0.5px; writing-mode: vertical-rl; text-orientation: mixed; transform: rotate(180deg); }
    .bottom-bar { height: 1.5mm; background: linear-gradient(90deg, {{branding.custom.accentColour}}, transparent); flex-shrink: 0; }
    """;

static PdfOptions A6PdfOptions() => new()
{
    Width = "105mm", Height = "148mm", PrintBackground = true,
    Margins = new PdfMargins { Top = "0mm", Bottom = "0mm", Left = "0mm", Right = "0mm" }
};

static PdfOptions CreditCardPdfOptions() => new()
{
    Width = "85.6mm", Height = "54mm", PrintBackground = true,
    Margins = new PdfMargins { Top = "0mm", Bottom = "0mm", Left = "0mm", Right = "0mm" }
};

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

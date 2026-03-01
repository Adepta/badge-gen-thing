using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace DocumentGenerator.Console.Logging;

/// <summary>
/// Owns the terminal for the lifetime of the host, rendering a live TUI via
/// <see cref="AnsiConsole.Live"/>: a header rule, a scrolling log panel fed by
/// <see cref="LogBuffer"/>, and a status bar with uptime and render counts.
///
/// Press Q (or Ctrl+C) to trigger a graceful host shutdown.
/// </summary>
public sealed class TuiRenderer : IHostedService, IDisposable
{
    private const int VisibleLines = 30;
    private const int DebounceMs   = 80;

    private readonly LogBuffer              _buffer;
    private readonly RenderStats            _stats;
    private readonly IHostApplicationLifetime _lifetime;

    private CancellationTokenSource? _cts;
    private Task?                     _renderTask;
    private Task?                     _keyTask;

    public TuiRenderer(LogBuffer buffer, RenderStats stats, IHostApplicationLifetime lifetime)
    {
        _buffer   = buffer;
        _stats    = stats;
        _lifetime = lifetime;
    }

    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cts        = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _renderTask = Task.Run(() => RunAsync(_cts.Token), CancellationToken.None);
        _keyTask    = Task.Run(() => WatchKeysAsync(_cts.Token), CancellationToken.None);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_cts is not null)
        {
            await _cts.CancelAsync();
            _cts.Dispose();
            _cts = null;
        }

        foreach (var t in new[] { _renderTask, _keyTask })
        {
            if (t is null) continue;
            try { await t.WaitAsync(TimeSpan.FromSeconds(3), cancellationToken); }
            catch (OperationCanceledException) { }
        }

        _renderTask = null;
        _keyTask    = null;
    }

    /// <inheritdoc/>
    public void Dispose() => _cts?.Dispose();

    // ── Key watcher ───────────────────────────────────────────────────────────

    private async Task WatchKeysAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                // Poll every 100 ms so we don't block forever on KeyAvailable
                if (System.Console.KeyAvailable)
                {
                    var key = System.Console.ReadKey(intercept: true);
                    if (key.Key == ConsoleKey.Q)
                    {
                        _buffer.Add(new LogBuffer.LogEntry(
                            DateTime.Now,
                            Microsoft.Extensions.Logging.LogLevel.Information,
                            "TuiRenderer",
                            "Q pressed — shutting down gracefully..."));

                        _lifetime.StopApplication();
                        return;
                    }
                }

                await Task.Delay(100, ct);
            }
        }
        catch (OperationCanceledException) { /* normal shutdown */ }
    }

    // ── Render loop ───────────────────────────────────────────────────────────

    private async Task RunAsync(CancellationToken ct)
    {
        try
        {
            await AnsiConsole.Live(BuildLayout())
                .AutoClear(false)
                .Overflow(VerticalOverflow.Ellipsis)
                .StartAsync(async ctx =>
                {
                    while (!ct.IsCancellationRequested)
                    {
                        try
                        {
                            await _buffer.WaitAsync(TimeSpan.FromSeconds(1), ct).AsTask();
                        }
                        catch (OperationCanceledException) { break; }

                        if (ct.IsCancellationRequested) break;

                        try { await Task.Delay(DebounceMs, ct); }
                        catch (OperationCanceledException) { break; }

                        ctx.UpdateTarget(BuildLayout());
                    }
                });
        }
        catch (OperationCanceledException) { /* normal shutdown */ }
    }

    // ── Layout builders ───────────────────────────────────────────────────────

    private Table BuildLayout()
    {
        var root = new Table().Border(TableBorder.None).HideHeaders().Expand();
        root.AddColumn(new TableColumn(string.Empty));
        root.AddRow(new Rule("[bold purple] Document Generator [/]  [grey]render service[/]").RuleStyle(Style.Parse("grey")));
        root.AddRow(BuildLogPanel());
        root.AddRow(BuildStatusBar());
        return root;
    }

    private Panel BuildLogPanel()
    {
        var entries = _buffer.Snapshot();
        var visible = entries.Count > VisibleLines
            ? entries.Skip(entries.Count - VisibleLines).ToList()
            : (IReadOnlyList<LogBuffer.LogEntry>)entries;

        var grid = new Grid().AddColumn();

        if (visible.Count == 0)
        {
            grid.AddRow(new Markup("[grey35]  Waiting for log entries...[/]"));
        }
        else
        {
            foreach (var entry in visible)
            {
                var (dot, badge) = entry.Level switch
                {
                    LogLevel.Trace       => ("[grey35]·[/]",  "[grey35] TRCE [/]"),
                    LogLevel.Debug       => ("[grey53]·[/]",  "[grey53] DBUG [/]"),
                    LogLevel.Information => ("[green]●[/]",   "[bold green] INFO [/]"),
                    LogLevel.Warning     => ("[yellow]▲[/]",  "[bold yellow] WARN [/]"),
                    LogLevel.Error       => ("[red]✕[/]",     "[bold red on #1a0000] FAIL [/]"),
                    LogLevel.Critical    => ("[red bold]‼[/]","[bold white on red] CRIT [/]"),
                    _                    => ("[grey]·[/]",    "[grey]     [/]")
                };

                var msgStyle = entry.Level switch
                {
                    LogLevel.Trace or LogLevel.Debug        => "[grey35]{0}[/]",
                    LogLevel.Warning                        => "[yellow]{0}[/]",
                    LogLevel.Error or LogLevel.Critical     => "[red]{0}[/]",
                    _                                       => "[white]{0}[/]"
                };

                grid.AddRow(new Markup($" [grey35]{entry.Timestamp:HH:mm:ss}[/]  {dot} {badge}  {string.Format(msgStyle, entry.Message)}"));
                grid.AddRow(new Markup($"[grey35]           · {entry.Category}[/]"));

                if (entry.Exception is not null)
                    grid.AddRow(new Markup($"[red]           {EscapeMarkup(entry.Exception.Message)}[/]"));
            }
        }

        return new Panel(grid).Header("[grey] Log [/]").BorderColor(Color.Grey35).Expand();
    }

    private Markup BuildStatusBar()
    {
        var u = _stats.Uptime;
        var uptime = u.TotalHours >= 1
            ? $"{(int)u.TotalHours:D2}:{u.Minutes:D2}:{u.Seconds:D2}"
            : $"{u.Minutes:D2}:{u.Seconds:D2}";

        return new Markup(
            $"[grey35] uptime [white]{uptime}[/]   " +
            $"ok [bold green]{_stats.SuccessCount}[/]   " +
            $"fail [bold red]{_stats.FailureCount}[/]   " +
            $"[grey35]Q to quit   Ctrl+C to force stop[/] [/]");
    }

    private static string EscapeMarkup(string text) =>
        text.Replace("[", "[[").Replace("]", "]]");
}

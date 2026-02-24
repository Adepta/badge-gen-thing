using Microsoft.Extensions.Logging;

namespace DocumentGenerator.Console.Logging;

/// <summary>
/// An <see cref="ILogger"/> implementation that appends entries to a shared
/// <see cref="LogBuffer"/> for consumption by the TUI renderer.
///
/// The logger does NOT write directly to <c>AnsiConsole</c> — the
/// <see cref="TuiRenderer"/> hosted service owns the console and renders
/// the buffer on each refresh tick.
/// </summary>
internal sealed class SpectreConsoleLogger(
    string categoryName,
    LogLevel minimumLevel,
    LogBuffer buffer) : ILogger
{
    private readonly string _shortCategory = ShortenCategory(categoryName);

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= minimumLevel;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;

        var message = EscapeMarkup(formatter(state, exception));

        buffer.Add(new LogBuffer.LogEntry(
            Timestamp: DateTime.Now,
            Level:     logLevel,
            Category:  _shortCategory,
            Message:   message,
            Exception: exception));
    }

    // Strips "DocumentGenerator." prefix and abbreviates namespace segments so the
    // category fits in the 48-char budget without wrapping the log line.
    private static string ShortenCategory(string category)
    {
        const string rootPrefix = "DocumentGenerator.";
        const int    maxLength  = 48;

        var trimmed = category.StartsWith(rootPrefix, StringComparison.Ordinal)
            ? category[rootPrefix.Length..]
            : category;

        if (trimmed.Length <= maxLength) return trimmed;

        var parts = trimmed.Split('.');
        if (parts.Length <= 1) return trimmed[^maxLength..];

        var abbreviated = parts[..^1]
            .Select(p => p.Length > 3 ? p[..1] : p)
            .Append(parts[^1]);

        var result = string.Join(".", abbreviated);
        return result.Length <= maxLength ? result : result[^maxLength..];
    }

    private static string EscapeMarkup(string text) =>
        text.Replace("[", "[[").Replace("]", "]]");
}

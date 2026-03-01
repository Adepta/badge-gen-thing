using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace DocumentGenerator.Console.Logging;

/// <summary>
/// Bounded ring of log entries shared between <see cref="SpectreConsoleLogger"/> (writer)
/// and <see cref="TuiRenderer"/> (reader). The channel wakes the renderer on each write;
/// <see cref="Snapshot"/> returns a point-in-time copy for display.
/// </summary>
public sealed class LogBuffer
{
    /// <summary>An immutable snapshot of a single log event written to the buffer.</summary>
    /// <param name="Timestamp">Local time the log entry was created.</param>
    /// <param name="Level">Severity level of the log event.</param>
    /// <param name="Category">Shortened logger category (type name).</param>
    /// <param name="Message">Formatted log message with Spectre markup already escaped.</param>
    /// <param name="Exception">Optional exception attached to the log event.</param>
    public record LogEntry(
        DateTime Timestamp,
        LogLevel Level,
        string Category,
        string Message,
        Exception? Exception = null);

    private readonly int _capacity;
    private readonly LogEntry[] _ring;
    private int _head;  // next write position
    private int _count;
    private readonly object _lock = new();

    // Unbounded channel used purely as a wake signal — the renderer re-reads
    // _ring on each tick rather than consuming individual entries from the channel.
    private readonly Channel<byte> _signal =
        Channel.CreateBounded<byte>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true
        });

    /// <summary>
    /// Creates a new buffer that retains the most recent <paramref name="capacity"/> entries.
    /// Older entries are silently overwritten when the ring fills.
    /// </summary>
    /// <param name="capacity">Maximum number of entries to retain. Defaults to <c>500</c>.</param>
    public LogBuffer(int capacity = 500)
    {
        _capacity = capacity;
        _ring     = new LogEntry[capacity];
    }

    /// <summary>
    /// Appends <paramref name="entry"/> to the ring buffer and signals any waiting readers.
    /// Thread-safe; may be called from multiple logger instances concurrently.
    /// </summary>
    /// <param name="entry">The log entry to append.</param>
    public void Add(LogEntry entry)
    {
        lock (_lock)
        {
            _ring[_head] = entry;
            _head        = (_head + 1) % _capacity;
            if (_count < _capacity) _count++;
        }

        _signal.Writer.TryWrite(0);
    }

    /// <summary>
    /// Waits until a new entry is available or <paramref name="timeout"/> elapses.
    /// Used by the renderer to block rather than busy-poll.
    /// </summary>
    public async ValueTask<bool> WaitAsync(TimeSpan timeout, CancellationToken ct)
    {
        try
        {
            return await _signal.Reader.WaitToReadAsync(ct).AsTask().WaitAsync(timeout, ct);
        }
        catch (TimeoutException) { return false; }
    }

    /// <summary>Snapshot of the current entries, oldest-first.</summary>
    public IReadOnlyList<LogEntry> Snapshot()
    {
        lock (_lock)
        {
            if (_count == 0) return Array.Empty<LogEntry>();

            var result = new LogEntry[_count];
            var start  = _count == _capacity ? _head : 0;
            for (int i = 0; i < _count; i++)
                result[i] = _ring[(start + i) % _capacity];
            return result;
        }
    }
}

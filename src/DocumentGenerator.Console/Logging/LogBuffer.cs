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

    public LogBuffer(int capacity = 500)
    {
        _capacity = capacity;
        _ring     = new LogEntry[capacity];
    }

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

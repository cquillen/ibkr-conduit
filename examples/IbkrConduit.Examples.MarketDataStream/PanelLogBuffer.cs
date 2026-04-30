using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace IbkrConduit.Examples.MarketDataStream;

/// <summary>
/// In-memory <see cref="ILoggerProvider"/> that captures formatted log messages into a
/// fixed-capacity ring buffer for rendering inside a Spectre.Console <c>Panel</c>.
/// The render loop snapshots this buffer on every refresh tick.
/// </summary>
/// <remarks>
/// Capacity is intentionally small (matches the panel height) — older entries are
/// dropped on overflow. For full diagnostic capture, use <c>--log-file</c>, which
/// routes to <see cref="FileLoggerProvider"/> in parallel.
/// </remarks>
internal sealed class PanelLogBuffer : ILoggerProvider
{
    private const int _capacity = 8;
    private readonly ConcurrentQueue<LogEntry> _entries = new();

    /// <summary>Creates a per-category logger that appends into the shared buffer.</summary>
    public ILogger CreateLogger(string categoryName) => new BufferingLogger(this);

    /// <summary>No unmanaged resources held; nothing to release.</summary>
    public void Dispose()
    {
        // Intentionally empty — the ConcurrentQueue holds only managed memory.
    }

    /// <summary>Appends an entry, evicting the oldest entries beyond <see cref="_capacity"/>.</summary>
    internal void Append(LogLevel level, string message)
    {
        _entries.Enqueue(new LogEntry(DateTimeOffset.UtcNow, level, message));
        while (_entries.Count > _capacity && _entries.TryDequeue(out _))
        {
            // Drop oldest — by design, the panel only shows the most recent N entries.
        }
    }

    /// <summary>
    /// Returns a point-in-time snapshot of the current entries, oldest first.
    /// May briefly observe a concurrent append; the next render tick self-corrects.
    /// </summary>
    internal IReadOnlyList<LogEntry> Snapshot() => _entries.ToArray();

    /// <summary>A single captured log entry.</summary>
    internal readonly record struct LogEntry(DateTimeOffset Timestamp, LogLevel Level, string Message);

    /// <summary>Per-category logger that forwards formatted messages to the shared buffer.</summary>
    private sealed class BufferingLogger(PanelLogBuffer buffer) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            // Per design: only the formatted message goes to the panel.
            // The exception parameter is intentionally not appended — the
            // file logger captures the full stack when --log-file is set.
            buffer.Append(logLevel, formatter(state, exception));
        }
    }
}

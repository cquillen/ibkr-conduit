using System.Globalization;
using Microsoft.Extensions.Logging;

namespace IbkrConduit.Examples.MarketDataStream;

/// <summary>
/// A minimal <see cref="ILoggerProvider"/> that appends log lines to a file.
/// Defers level filtering to the framework's filter chain (configured via
/// <c>SetMinimumLevel</c> in <c>Program.cs</c>) so callers control verbosity
/// uniformly. Auto-flushes on each write so a Ctrl+C exit doesn't lose the
/// trailing lines.
/// </summary>
internal sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly StreamWriter _writer;
    private readonly object _lock = new();

    public FileLoggerProvider(string path)
    {
        var stream = File.Open(path, FileMode.Append, FileAccess.Write, FileShare.Read);
        _writer = new StreamWriter(stream)
        {
            AutoFlush = true,
        };
    }

    public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName);

    public void Dispose()
    {
        lock (_lock)
        {
            _writer.Dispose();
        }
    }

    internal void Write(LogLevel level, string category, EventId eventId, string message, Exception? exception)
    {
        var timestamp = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
        var levelStr = level.ToString().ToLowerInvariant();
        lock (_lock)
        {
            _writer.Write(timestamp);
            _writer.Write(" [");
            _writer.Write(levelStr);
            _writer.Write("] ");
            _writer.Write(category);
            if (eventId.Id != 0 || !string.IsNullOrEmpty(eventId.Name))
            {
                _writer.Write('[');
                _writer.Write(eventId.Id);
                if (!string.IsNullOrEmpty(eventId.Name))
                {
                    _writer.Write('/');
                    _writer.Write(eventId.Name);
                }
                _writer.Write(']');
            }
            _writer.Write(": ");
            _writer.WriteLine(message);
            if (exception is not null)
            {
                _writer.WriteLine(exception);
            }
        }
    }
}

/// <summary>Per-category logger that forwards to the shared <see cref="FileLoggerProvider"/>.</summary>
internal sealed class FileLogger : ILogger
{
    private readonly FileLoggerProvider _provider;
    private readonly string _category;

    public FileLogger(FileLoggerProvider provider, string category)
    {
        _provider = provider;
        _category = category;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        var message = formatter(state, exception);
        _provider.Write(logLevel, _category, eventId, message, exception);
    }
}

using Microsoft.Extensions.Logging;

namespace Commerce.IntegrationTests.Infrastructure;

/// <summary>
/// Captures one host's log per start generation so startup assertions can prove the static
/// handler registry was attached exactly once and never fell back to an assembly scan.
/// </summary>
internal sealed class HostLogCapture : ILoggerProvider
{
    private const int Capacity = 50_000;
    private readonly Lock _gate = new();
    private readonly List<Entry> _entries = [];
    private int _phase;

    /// <inheritdoc/>
    public ILogger CreateLogger(string categoryName) => new CaptureLogger(this, categoryName);

    /// <inheritdoc/>
    public void Dispose()
    {
    }

    /// <summary>Starts a new generation; log records that follow belong to it.</summary>
    /// <returns>The one-based generation identifier.</returns>
    internal int BeginStart()
    {
        lock (_gate)
        {
            _phase++;
            return _phase;
        }
    }

    internal IReadOnlyList<string> Messages(int phase)
    {
        lock (_gate)
        {
            return [.. _entries.Where(entry => entry.Phase == phase).Select(entry => entry.Message)];
        }
    }

    internal IReadOnlyList<string> AllMessages()
    {
        lock (_gate)
        {
            return [.. _entries.Select(entry => entry.Message)];
        }
    }

    private void Record(string message)
    {
        lock (_gate)
        {
            if (_entries.Count < Capacity)
            {
                _entries.Add(new Entry(_phase, message));
            }
        }
    }

    private readonly struct Entry(int phase, string message)
    {
        internal int Phase { get; } = phase;

        internal string Message { get; } = message;
    }

    private sealed class CaptureLogger(HostLogCapture owner, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);
            owner.Record($"{category}|{logLevel}|{formatter(state, exception)}");
        }
    }
}

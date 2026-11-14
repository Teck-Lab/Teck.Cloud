using Serilog.Core;
using Serilog.Events;

namespace SharedKernel.Infrastructure.Hosting;

/// <summary>Converts raw exceptions to properties so masking covers both outputs.</summary>
/// <param name="output">The owned masking logger and its output sinks.</param>
internal sealed class ExceptionOutputSink(Logger output) : ILogEventSink, IDisposable
{
    public void Emit(LogEvent logEvent)
    {
        if (logEvent.Exception is null)
        {
            output.Write(logEvent);
            return;
        }

        var exception = logEvent.Exception;
        var safe = new LogEvent(
            logEvent.Timestamp,
            logEvent.Level,
            exception: null,
            logEvent.MessageTemplate,
            logEvent.Properties.Select(property => new LogEventProperty(property.Key, property.Value)),
            logEvent.TraceId ?? default,
            logEvent.SpanId ?? default);
        safe.AddOrUpdateProperty(new LogEventProperty("exception.type", new ScalarValue(exception.GetType().FullName)));
        safe.AddOrUpdateProperty(new LogEventProperty("exception.message", new ScalarValue(exception.Message)));
        safe.AddOrUpdateProperty(new LogEventProperty("exception.stacktrace", new ScalarValue(exception.ToString())));
        safe.AddOrUpdateProperty(new LogEventProperty("ExceptionText", new ScalarValue(exception.ToString())));
        output.Write(safe);
    }

    public void Dispose() => output.Dispose();
}

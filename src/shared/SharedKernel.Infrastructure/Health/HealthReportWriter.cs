using System.Buffers;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SharedKernel.Infrastructure.Health;

/// <summary>Exports only safe statuses and timings, never exceptions, connection details or check data.</summary>
internal static class HealthReportWriter
{
    internal static async Task WriteAsync(HttpContext context, HealthReport report)
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new Utf8JsonWriter(buffer);
        await using (var disposeWriter = writer.ConfigureAwait(false))
        {
            writer.WriteStartObject();
            writer.WriteString("status", report.Status.ToString());
            writer.WriteStartObject("checks");
            foreach (var entry in report.Entries)
            {
                writer.WriteStartObject(entry.Key);
                writer.WriteString("status", entry.Value.Status.ToString());
                writer.WriteNumber("durationMs", entry.Value.Duration.TotalMilliseconds);
                writer.WriteEndObject();
            }

            writer.WriteEndObject();
            writer.WriteEndObject();
            await writer.FlushAsync(context.RequestAborted).ConfigureAwait(false);
        }

        context.Response.ContentType = "application/json";
        await context.Response.Body.WriteAsync(buffer.WrittenMemory, context.RequestAborted).ConfigureAwait(false);
    }
}

using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using Serilog;
using Serilog.Enrichers.Sensitive;
using Serilog.Sinks.OpenTelemetry;

namespace SharedKernel.Infrastructure.Hosting;

internal static class LogOutput
{
    public static ExceptionOutputSink Create(IServiceProvider services, Resource resource, OtlpTarget? target)
    {
        var output = new LoggerConfiguration().MinimumLevel.Verbose()
            .Enrich.WithSensitiveDataMasking(LogEnrichment.ConfigureMasking)
            .WriteTo.Async(
                sink => sink.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss.fff} {Level:u3}] {ServiceName} {SourceContext} [trace={TraceId} span={SpanId}] {Message:lj} {Properties:j}{NewLine}{ExceptionText:l}",
                    formatProvider: CultureInfo.InvariantCulture),
                bufferSize: 10000,
                blockWhenFull: false,
                monitor: services.GetRequiredService<AsyncConsoleMonitor>());
        if (target is not null)
        {
            _ = output.WriteTo.OpenTelemetry(options => ConfigureOtlp(options, resource, target), ignoreEnvironment: true);
        }

        return new ExceptionOutputSink(output.CreateLogger());
    }

    private static void ConfigureOtlp(BatchedOpenTelemetrySinkOptions options, Resource resource, OtlpTarget target)
    {
        options.Endpoint = target.Endpoint.AbsoluteUri;
        options.LogsEndpoint = target.Endpoint.AbsoluteUri;
        options.Protocol = target.Protocol == OtlpExportProtocol.Grpc ? OtlpProtocol.Grpc : OtlpProtocol.HttpProtobuf;
        options.Headers = target.HeaderDictionary();
        options.ResourceAttributes = resource.Attributes.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
        options.IncludedData = IncludedData.TraceIdField | IncludedData.SpanIdField | IncludedData.MessageTemplateTextAttribute | IncludedData.SpecRequiredResourceAttributes;
        options.OnBeginSuppressInstrumentation = SuppressInstrumentationScope.Begin;
    }
}

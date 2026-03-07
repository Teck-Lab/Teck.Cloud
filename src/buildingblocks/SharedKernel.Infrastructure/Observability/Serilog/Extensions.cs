using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Exceptions;
using Serilog.Exceptions.Core;
using Serilog.Exceptions.EntityFrameworkCore.Destructurers;
using Serilog.Formatting.Compact;
using Serilog.Sinks.OpenTelemetry;
using Serilog.Sinks.SystemConsole.Themes;
using SharedKernel.Infrastructure.Options;

namespace SharedKernel.Infrastructure.Observability.Serilog;

internal static class Extensions
{
    internal static IHostApplicationBuilder ConfigureTeckCloudSerilog(this IHostApplicationBuilder builder)
    {
        SerilogOptions serilogOptions = builder.Services.BindValidateReturn<SerilogOptions>(builder.Configuration);
        string appName = builder.Environment.ApplicationName;

        builder.Services.AddSerilog((_, loggerConfiguration) =>
        {
            if (serilogOptions.EnableErichers)
            {
                ConfigureEnrichers(loggerConfiguration, appName);
            }

            ConfigureConsoleLogging(loggerConfiguration, serilogOptions.StructuredConsoleLogging);
            ConfigureWriteToFile(loggerConfiguration, serilogOptions.WriteToFile, serilogOptions.RetentionFileCount, appName);
            SetMinimumLogLevel(loggerConfiguration, serilogOptions.MinimumLogLevel);
            if (serilogOptions.OverideMinimumLogLevel)
            {
                OverideMinimumLogLevel(loggerConfiguration);
            }

            string? otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
            if (!string.IsNullOrWhiteSpace(otlpEndpoint))
            {
                loggerConfiguration.WriteTo.OpenTelemetry(options =>
                {
                    options.Endpoint = otlpEndpoint;
                    options.Protocol = OtlpProtocol.Grpc;
                    options.IncludedData = IncludedData.TraceIdField |
                                           IncludedData.SpanIdField |
                                           IncludedData.MessageTemplateTextAttribute |
                                           IncludedData.SpecRequiredResourceAttributes;
                    options.ResourceAttributes = new Dictionary<string, object>
                    {
                        ["service.name"] = builder.Environment.ApplicationName,
                        ["service.version"] = typeof(Extensions).Assembly.GetName().Version?.ToString() ?? "1.0.0",
                        ["deployment.environment"] = builder.Environment.EnvironmentName,
                    };
                });
            }
        });

        return builder;
    }

    private static void ConfigureEnrichers(LoggerConfiguration config, string appName)
    {
        config
            .Enrich.FromLogContext()
            .Enrich.WithProperty("ApplicationName", appName)
            .Enrich.WithExceptionDetails(new DestructuringOptionsBuilder().WithDefaultDestructurers().WithDestructurers([new DbUpdateExceptionDestructurer()]))
            .Enrich.WithMachineName()
            .Enrich.WithEnvironmentName()
            .Enrich.WithProcessId()
            .Enrich.WithProcessName()
            .Enrich.WithThreadId()
            .Enrich.WithThreadName()
            .Enrich.WithProperty("TraceId", () => Activity.Current?.TraceId.ToString() ?? "");
    }

    private static void ConfigureConsoleLogging(LoggerConfiguration serilogConfig, bool structuredConsoleLogging)
    {
        if (structuredConsoleLogging)
        {
            serilogConfig.WriteTo.Async(wt => wt.Console(new CompactJsonFormatter()));
        }
        else
        {
            serilogConfig.WriteTo.Async(consoleWriter => consoleWriter.Console(theme: AnsiConsoleTheme.Literate, outputTemplate:
                    "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}"));
        }
    }

    private static void ConfigureWriteToFile(LoggerConfiguration serilogConfig, bool writeToFile, int retainedFileCount, string appName)
    {
        if (writeToFile)
        {
            serilogConfig.WriteTo.File(
                new CompactJsonFormatter(),
                $"Logs/{appName.ToLowerInvariant()}.logs.json",
                restrictedToMinimumLevel: LogEventLevel.Information,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: retainedFileCount);
        }
    }

    private static void SetMinimumLogLevel(LoggerConfiguration serilogConfig, string minLogLevel)
    {
        LoggingLevelSwitch loggingLevelSwitch = new()
        {
            MinimumLevel = minLogLevel.ToLowerInvariant() switch
            {
                "debug" => LogEventLevel.Debug,
                "information" => LogEventLevel.Information,
                "warning" => LogEventLevel.Warning,
                _ => LogEventLevel.Information,
            }
        };

        serilogConfig.MinimumLevel.ControlledBy(loggingLevelSwitch);
    }

    private static void OverideMinimumLogLevel(LoggerConfiguration serilogConfig)
    {
        serilogConfig
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Hangfire", LogEventLevel.Warning)
            .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
            .MinimumLevel.Override("OpenIddict.Validation", LogEventLevel.Error)
            .MinimumLevel.Override("System.Net.Http.HttpClient.OpenIddict", LogEventLevel.Error)
            .MinimumLevel.Override("Yarp", LogEventLevel.Warning);
    }
}

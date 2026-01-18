using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Exceptions;
using Serilog.Exceptions.Core;
using Serilog.Exceptions.EntityFrameworkCore.Destructurers;
using Serilog.Formatting.Compact;
using SharedKernel.Infrastructure.Options;

namespace SharedKernel.Infrastructure.Logging
{
    /// <summary>
    /// The extensions.
    /// </summary>
    public static class Extensions
    {
        /// <summary>
        /// Configures the serilog.
        /// </summary>
        /// <param name="builder">The builder.</param>
        /// <param name="appName">The app name.</param>
        public static void ConfigureSerilog(this WebApplicationBuilder builder, string appName)
        {
            Microsoft.Extensions.Configuration.ConfigurationManager config = builder.Configuration;
            SerilogOptions serilogOptions = builder.Services.BindValidateReturn<SerilogOptions>(config);
            _ = builder.Host.UseSerilog((_, _, serilogConfig) =>
            {
                serilogConfig.WriteTo.OpenTelemetry();
                if (serilogOptions.EnableErichers)
                {
                    ConfigureEnrichers(serilogConfig, appName);
                }

                ConfigureConsoleLogging(serilogConfig, serilogOptions.StructuredConsoleLogging);
                ConfigureWriteToFile(serilogConfig, serilogOptions.WriteToFile, serilogOptions.RetentionFileCount, appName);
                SetMinimumLogLevel(serilogConfig, serilogOptions.MinimumLogLevel);
                if (serilogOptions.OverideMinimumLogLevel)
                {
                    OverideMinimumLogLevel(serilogConfig);
                }
            });
        }

        /// <summary>
        /// Configures the enrichers.
        /// </summary>
        /// <param name="config">The config.</param>
        /// <param name="appName">The app name.</param>
        private static void ConfigureEnrichers(LoggerConfiguration config, string appName)
        {
            config
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Application", appName)
                .Enrich.WithExceptionDetails(new DestructuringOptionsBuilder().WithDefaultDestructurers().WithDestructurers([new DbUpdateExceptionDestructurer()]))
                .Enrich.WithMachineName()
                .Enrich.WithProcessId()
                .Enrich.WithThreadId()
                .Enrich.WithCorrelationId()
                .Enrich.WithProperty("TraceId", () => Activity.Current?.TraceId.ToString() ?? "");
        }

        /// <summary>
        /// Configures console logging.
        /// </summary>
        /// <param name="serilogConfig">The serilog config.</param>
        /// <param name="structuredConsoleLogging">If true, structured console logging.</param>
        private static void ConfigureConsoleLogging(LoggerConfiguration serilogConfig, bool structuredConsoleLogging)
        {
            if (structuredConsoleLogging)
            {
                serilogConfig.WriteTo.Async(wt => wt.Console(new CompactJsonFormatter()));
            }
            else
            {
                serilogConfig.WriteTo.Async(wt => wt.Console());
            }
        }

        /// <summary>
        /// Configures write converts to file.
        /// </summary>
        /// <param name="serilogConfig">The serilog config.</param>
        /// <param name="writeToFile">If true, write converts to file.</param>
        /// <param name="retainedFileCount">The retained file count.</param>
        /// <param name="appName">The app name.</param>
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

        /// <summary>
        /// Set minimum log level.
        /// </summary>
        /// <param name="serilogConfig">The serilog config.</param>
        /// <param name="minLogLevel">The min log level.</param>
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

        /// <summary>
        /// Overides minimum log level.
        /// </summary>
        /// <param name="serilogConfig">The serilog config.</param>
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
}

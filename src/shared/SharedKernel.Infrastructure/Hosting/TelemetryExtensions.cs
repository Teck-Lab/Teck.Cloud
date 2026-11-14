using System.Globalization;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Pyroscope.OpenTelemetry;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using SharedKernel.Persistence.Caching;

namespace SharedKernel.Infrastructure.Hosting;

/// <summary>Shared request tracing, metrics and correlated Serilog output.</summary>
public static class TelemetryExtensions
{
    /// <summary>Registers instrumentation once; export activates when an OTLP endpoint is configured.</summary>
    /// <param name="builder">The owning application host.</param>
    /// <param name="exportTelemetry">False disables export; null selects it from endpoint configuration.</param>
    /// <exception cref="InvalidOperationException">Telemetry is duplicated or required export has no endpoint.</exception>
    public static void AddFoundationTelemetry(this IHostApplicationBuilder builder, bool? exportTelemetry = null)
    {
        if (builder.Services.Any(service => service.ServiceType == typeof(Registration)))
        {
            throw new InvalidOperationException("Register foundation telemetry once per host.");
        }

        var traces = OtlpTarget.Read(builder.Configuration, "traces", exportTelemetry);
        var metrics = OtlpTarget.Read(builder.Configuration, "metrics", exportTelemetry);
        var logs = OtlpTarget.Read(builder.Configuration, "logs", exportTelemetry);
        if (exportTelemetry is true && traces is null && metrics is null && logs is null)
        {
            throw new InvalidOperationException("Telemetry export requires an OTLP endpoint.");
        }

        var resource = BuildResource(builder);
        _ = builder.Services.AddSingleton(new Registration(resource));
        _ = builder.Services.AddSingleton<IRedisConnectionObserver, RedisTelemetryObserver>();
        _ = builder.Services.AddOpenTelemetry()
            .ConfigureResource(configure => configure.AddAttributes(resource.Attributes))
            .WithTracing(tracing => ConfigureTracing(tracing, builder, traces))
            .WithMetrics(meter => ConfigureMetrics(meter, builder.Environment.ApplicationName, metrics));
        ConfigureLogging(builder, resource, logs);
    }

    private static Resource BuildResource(IHostApplicationBuilder builder) => ResourceBuilder.CreateDefault()
        .AddContainerDetector()
        .AddProcessDetector()
        .AddProcessRuntimeDetector()
        .AddOperatingSystemDetector()
        .AddService(
            builder.Configuration["OTEL_SERVICE_NAME"] ?? builder.Environment.ApplicationName,
            serviceVersion: builder.Configuration["Telemetry:ServiceVersion"] ?? Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "unknown",
            serviceInstanceId: builder.Configuration["Telemetry:ServiceInstanceId"] ?? Guid.NewGuid().ToString("N"))
        .AddAttributes([new KeyValuePair<string, object>("deployment.environment.name", builder.Environment.EnvironmentName)])
        .Build();

    private static void ConfigureTracing(TracerProviderBuilder tracing, IHostApplicationBuilder builder, OtlpTarget? target)
    {
        _ = tracing.AddSource(builder.Environment.ApplicationName, "Teck.Cloud", "Wolverine", "RabbitMQ.Client")
            .SetSampler(CreateSampler(builder.Configuration))
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddGrpcClientInstrumentation(options => options.SuppressDownstreamInstrumentation = true)
            .AddEntityFrameworkCoreInstrumentation(options => options.Filter = (provider, _) =>
                !string.Equals(provider, "Npgsql.EntityFrameworkCore.PostgreSQL", StringComparison.Ordinal))
            .AddNpgsql()
            .AddRedisInstrumentation(options =>
            {
                options.SetVerboseDatabaseStatements = false;
                options.FlushInterval = TimeSpan.FromSeconds(1);
            })
            .AddFusionCacheInstrumentation(options =>
            {
                options.IncludeMemoryLevel = true;
                options.IncludeDistributedLevel = true;
                options.IncludeBackplane = true;
            });
        if (builder.Configuration.GetValue<bool>("Telemetry:Pyroscope:Enabled"))
        {
            _ = tracing.AddProcessor<PyroscopeSpanProcessor>();
        }

        if (target is not null)
        {
            _ = tracing.AddOtlpExporter(target.Configure);
        }
    }

    private static void ConfigureMetrics(MeterProviderBuilder metrics, string application, OtlpTarget? target)
    {
        _ = metrics.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation().AddNpgsqlInstrumentation()
            .AddProcessInstrumentation()
            .AddView("process.cpu.time", MetricStreamConfiguration.Drop)
            .AddView("process.memory.usage", MetricStreamConfiguration.Drop)
            .AddMeter(application, "Teck.Cloud", "System.Runtime", "Microsoft.AspNetCore.Hosting", "Microsoft.AspNetCore.Server.Kestrel", "System.Net.Http", "System.Net.NameResolution", "System.Net.Security", "System.Net.Sockets", "Microsoft.EntityFrameworkCore", "Npgsql", "StackExchange.Redis", "Grpc.Net.Client", "Grpc.AspNetCore.Server", "RabbitMQ.Client", "Wolverine*")
            .AddFusionCacheInstrumentation(options =>
            {
                options.IncludeMemoryLevel = true;
                options.IncludeDistributedLevel = true;
                options.IncludeBackplane = true;
            });

        if (target is not null)
        {
            _ = metrics.AddOtlpExporter(target.Configure);
        }
    }

    private static Sampler CreateSampler(IConfiguration configuration)
    {
        var name = configuration["OTEL_TRACES_SAMPLER"] ?? "parentbased_always_on";
        return name switch
        {
            "always_on" => new AlwaysOnSampler(),
            "always_off" => new AlwaysOffSampler(),
            "traceidratio" => new TraceIdRatioBasedSampler(Ratio(configuration)),
            "parentbased_always_on" => new ParentBasedSampler(new AlwaysOnSampler()),
            "parentbased_always_off" => new ParentBasedSampler(new AlwaysOffSampler()),
            "parentbased_traceidratio" => new ParentBasedSampler(new TraceIdRatioBasedSampler(Ratio(configuration))),
            _ => throw new InvalidOperationException("Unsupported OTEL_TRACES_SAMPLER."),
        };
    }

    private static double Ratio(IConfiguration configuration) =>
        double.Parse(configuration["OTEL_TRACES_SAMPLER_ARG"] ?? "1", CultureInfo.InvariantCulture);

    private static void ConfigureLogging(IHostApplicationBuilder builder, Resource resource, OtlpTarget? target)
    {
        if (builder.Configuration.GetSection("Serilog:WriteTo").Exists() || builder.Configuration.GetSection("Serilog:AuditTo").Exists())
        {
            throw new InvalidOperationException("Foundation logging owns its sinks; configure levels and enrichers without additional WriteTo/AuditTo sinks.");
        }

        _ = builder.Logging.ClearProviders();
        _ = builder.Services.AddSingleton<AsyncConsoleMonitor>();
        if (builder.Configuration.GetValue<bool>("Telemetry:ClientInfo:Enabled") || builder.Configuration.GetValue<bool>("Telemetry:ClientInfo:UserAgent"))
        {
            _ = builder.Services.AddHttpContextAccessor();
        }

        _ = builder.Services.AddSerilog((services, logger) =>
        {
            _ = logger.MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning)
                .ReadFrom.Configuration(builder.Configuration)
                .Enrich.With([.. services.GetServices<ILogEventEnricher>()])
                .Destructure.With([.. services.GetServices<IDestructuringPolicy>()])
                .Enrich.FromLogContext().Enrich.WithMachineName().Enrich.With<TenantTraceEnricher>()
                .Enrich.WithProperty("ServiceName", resource.Attributes.First(item => string.Equals(item.Key, "service.name", StringComparison.Ordinal)).Value)
                .Enrich.WithProperty("ServiceVersion", resource.Attributes.First(item => string.Equals(item.Key, "service.version", StringComparison.Ordinal)).Value)
                .Enrich.WithProperty("ServiceInstanceId", resource.Attributes.First(item => string.Equals(item.Key, "service.instance.id", StringComparison.Ordinal)).Value)
                .Enrich.WithProperty("Environment", builder.Environment.EnvironmentName)
                .WriteTo.Sink(LogOutput.Create(services, resource, target));
            LogEnrichment.Configure(logger);
            if (builder.Configuration.GetValue<bool>("Telemetry:ClientInfo:Enabled"))
            {
                _ = logger.Enrich.WithClientIp();
            }

            if (builder.Configuration.GetValue<bool>("Telemetry:ClientInfo:UserAgent"))
            {
                _ = logger.Enrich.WithRequestHeader("User-Agent", "UserAgent");
            }
        });
    }

    private sealed record Registration(Resource Resource);
}

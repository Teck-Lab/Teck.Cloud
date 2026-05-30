// <copyright file="InfrastructureServiceExtensions.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using System.Reflection;
using Device.Application.AccessPoints;
using Device.Application.Assignments.Abstractions;
using Device.Application.DeviceDefinitions.Abstractions;
using Device.Application.DeviceLayouts.Abstractions;
using Device.Application.Hanshow.Abstractions;
using Device.Application.Operations.Saga;
using Device.Domain.AccessPoints;
using Device.Infrastructure.AccessPoints;
using Device.Infrastructure.Assignments;
using Device.Infrastructure.Persistence;
using Device.Infrastructure.Persistence.Repositories.Read;
using Device.Infrastructure.Persistence.Repositories.Write;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using SharedKernel.Core.Exceptions;
using SharedKernel.Core.Pricing;
using SharedKernel.Infrastructure;
using SharedKernel.Infrastructure.HealthChecks;
using SharedKernel.Infrastructure.Messaging;
using SharedKernel.Persistence.Database;
using SharedKernel.Persistence.Database.EFCore;
using SharedKernel.Persistence.Database.MultiTenant;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.RDBMS;

namespace Device.Infrastructure.DependencyInjection;

/// <summary>
/// Registers Device infrastructure services.
/// </summary>
public static class InfrastructureServiceExtensions
{
    /// <summary>
    /// Adds Device infrastructure dependencies.
    /// </summary>
    /// <param name="builder">Application builder.</param>
    /// <param name="applicationAssembly">Application assembly.</param>
    public static void AddInfrastructureServices(this WebApplicationBuilder builder, Assembly applicationAssembly)
    {
        ValidateInputs(builder, applicationAssembly);
        bool isRunningGeneration = CodeGenerationDetector.IsRunningGeneration();
        DeviceConnectionSettings connectionSettings = ResolveConnectionSettings(builder.Configuration, isRunningGeneration);

        ConfigureDatabase(builder, connectionSettings);
        ConfigureWolverine(builder, applicationAssembly, connectionSettings, isRunningGeneration);
        ConfigureHealthChecks(builder, connectionSettings, isRunningGeneration);
        RegisterServices(builder.Services);
        ConfigureHttpClients(builder.Services, builder.Configuration);
    }

    /// <summary>
    /// Adds Device infrastructure middleware.
    /// </summary>
    /// <param name="app">Application builder.</param>
    /// <returns>Configured application builder.</returns>
    public static IApplicationBuilder UseInfrastructureServices(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app;
    }

    private static void RegisterServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<Device.Application.Assignments.Abstractions.IDeviceDefinitionReadRepository, DbDisplayLayoutContextRepository>();
        services.AddScoped<Device.Application.DeviceDefinitions.Abstractions.IDeviceDefinitionReadRepository, DbDeviceDefinitionReadRepository>();
        services.AddScoped<IDeviceDefinitionWriteRepository, DbDeviceDefinitionWriteRepository>();
        services.AddScoped<IDeviceLayoutReadRepository, DbDeviceLayoutReadRepository>();
        services.AddScoped<IDeviceLayoutWriteRepository, DbDeviceLayoutWriteRepository>();
        services.AddScoped<Device.Application.AccessPoints.EffectiveAccessPointResolver>();
        services.AddScoped<Device.Application.Assignments.Abstractions.IDisplayAssignmentWriteRepository, DbDisplayAssignmentWriteRepository>();
        services.AddScoped<Device.Application.Assignments.Abstractions.IDisplayAssignmentReadRepository, DbDisplayAssignmentReadRepository>();
        services.AddScoped<IAccessPointReadRepository, DbAccessPointReadRepository>();
        services.AddScoped<IAccessPointWriteRepository, DbAccessPointWriteRepository>();
        services.AddSingleton<ILocationNodeResolver, HttpLocationNodeResolver>();
        services.AddSingleton<ILocationTemplateContextRunner, HttpLocationTemplateContextRunner>();
        services.AddSingleton<IProductSnapshotRunner, InMemoryProductSnapshotRunner>();
        services.AddSingleton<ILabelRenderJobRunner, InMemoryLabelRenderJobRunner>();
        services.AddSingleton<IHanshowHeartbeatProcessor, InMemoryHanshowHeartbeatProcessor>();
    }

    private static void ConfigureHttpClients(IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpClient("location", (client) =>
        {
            string? locationBaseUrl = configuration["Services:LocationBaseUrl"];
            if (!string.IsNullOrWhiteSpace(locationBaseUrl) && Uri.TryCreate(locationBaseUrl, UriKind.Absolute, out Uri? uri))
            {
                client.BaseAddress = uri;
            }
        });
    }

    private static void ConfigureDatabase(WebApplicationBuilder builder, DeviceConnectionSettings settings)
    {
        Assembly? migrationsAssembly = null;

        builder.AddCqrsDatabase(
            migrationsAssembly,
            settings.WriteConnectionString,
            settings.ReadConnectionString,
            settings.DatabaseProvider);
    }

    private static void ConfigureWolverine(WebApplicationBuilder builder, Assembly applicationAssembly, DeviceConnectionSettings settings, bool isRunningGeneration)
    {
        bool isDevelopment = builder.Environment.IsDevelopment();

        if (isRunningGeneration)
        {
            builder.Host.UseWolverine(options =>
            {
                IncludeHandlerAssemblies(options, applicationAssembly);
                options.AddSagaType<DisplayOperationSaga>();
                options.CodeGeneration.TypeLoadMode = JasperFx.CodeGeneration.TypeLoadMode.Dynamic;

                options.Services.TryAddSingleton<IVaultTenantConnectionProvider>(_ => new NullVaultTenantConnectionProvider());
                options.Services.TryAddSingleton(new WolverineTenantConnectionSource(settings.WriteConnectionString));
            });

            return;
        }

        builder.Host.UseWolverine(options =>
        {
            IncludeHandlerAssemblies(options, applicationAssembly);
            options.AddSagaType<DisplayOperationSaga>();

            OpenBaoOptions openBaoOptions = builder.Configuration
                .GetSection(OpenBaoOptions.Section)
                .Get<OpenBaoOptions>() ?? new OpenBaoOptions();

            options.Services.TryAddSingleton<IVaultTenantConnectionProvider>(sp =>
            {
                if (string.IsNullOrWhiteSpace(openBaoOptions.Url))
                {
                    return new NullVaultTenantConnectionProvider();
                }

                ILogger<VaultTenantConnectionProvider> logger = sp.GetRequiredService<ILogger<VaultTenantConnectionProvider>>();
                return new VaultTenantConnectionProvider(openBaoOptions, "device", logger);
            });

            WolverineTenantConnectionSource tenantConnectionSource = new(settings.WriteConnectionString);
            options.Services.AddSingleton(tenantConnectionSource);

            Assembly? migrationsAssembly = null;
            options.Services.AddDbContextWithWolverineManagedMultiTenancy<DeviceWriteDbContext>(
                (dbBuilder, connectionString, tenantId) =>
                {
                    SharedKernel.Persistence.Database.Extensions.ConfigureProviderDbContextOptions(
                        dbBuilder,
                        connectionString.Value,
                        migrationsAssembly,
                        settings.DatabaseProvider);
                    dbBuilder.UseTeckCloudTenant(tenantId);
                },
                JasperFx.AutoCreate.CreateOrUpdate);

            WolverinePersistenceConfigurator.ConfigureStandardRuntime(
                options,
                isDevelopment,
                settings.DatabaseProvider,
                settings.WriteConnectionString,
                settings.RabbitConnectionString,
                tenantConnectionSource);

            Console.WriteLine($"[Startup] Using RabbitMQ URI for Wolverine: {settings.RabbitConnectionString}");
        });
    }

    private static void IncludeHandlerAssemblies(WolverineOptions options, Assembly applicationAssembly)
    {
        options.Discovery.IncludeAssembly(applicationAssembly);
        options.Discovery.IncludeAssembly(typeof(InfrastructureServiceExtensions).Assembly);

        Assembly? entryAssembly = Assembly.GetEntryAssembly();
        if (entryAssembly is not null)
        {
            options.Discovery.IncludeAssembly(entryAssembly);
        }
    }

    private static void ConfigureHealthChecks(WebApplicationBuilder builder, DeviceConnectionSettings settings, bool isRunningGeneration)
    {
        if (isRunningGeneration)
        {
            return;
        }

        builder.AddRabbitMqHealthCheck(settings.RabbitConnectionString);
    }

    private static DeviceConnectionSettings ResolveConnectionSettings(IConfiguration configuration, bool isRunningGeneration)
    {
        DatabaseProvider databaseProvider = configuration.GetDatabaseProvider();
        if (isRunningGeneration)
        {
            return CreateCodeGenerationConnectionSettings(databaseProvider);
        }

        string rabbitConnectionString = configuration.GetConnectionString("rabbitmq")
            ?? throw new ConfigurationMissingException("RabbitMq");
        string writeConnectionString = ResolveWriteConnectionString(configuration, databaseProvider);
        string readConnectionString = ResolveReadConnectionString(configuration, databaseProvider);

        return new DeviceConnectionSettings
        {
            RabbitConnectionString = WolverinePersistenceConfigurator.NormalizeRabbitConnectionString(rabbitConnectionString),
            WriteConnectionString = writeConnectionString,
            ReadConnectionString = readConnectionString,
            DatabaseProvider = databaseProvider,
        };
    }

    private static string ResolveReadConnectionString(IConfiguration configuration, DatabaseProvider provider)
    {
        _ = provider;
        return configuration.GetConnectionString("db-read")
            ?? ResolveWriteConnectionString(configuration, provider);
    }

    private static string ResolveWriteConnectionString(IConfiguration configuration, DatabaseProvider provider)
    {
        _ = provider;
        return configuration.GetConnectionString("db-write")
            ?? throw new ConfigurationMissingException("Database (write)");
    }

    private static DeviceConnectionSettings CreateCodeGenerationConnectionSettings(DatabaseProvider provider)
    {
        string placeholderConnectionString;
        if (provider == DatabaseProvider.SqlServer)
        {
            placeholderConnectionString = "Server=localhost,1433;Database=tempdb;User Id=sa;TrustServerCertificate=True";
        }
        else if (provider == DatabaseProvider.MySQL)
        {
            placeholderConnectionString = "Server=localhost;Port=3306;Database=tempdb;Uid=root;";
        }
        else
        {
            placeholderConnectionString = "Host=localhost;Port=5432;Database=tempdb;Username=postgres";
        }

        return new DeviceConnectionSettings
        {
            RabbitConnectionString = "amqp://guest:guest@localhost:5672/",
            WriteConnectionString = placeholderConnectionString,
            ReadConnectionString = placeholderConnectionString,
            DatabaseProvider = provider,
        };
    }

    private static void ValidateInputs(WebApplicationBuilder builder, Assembly applicationAssembly)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(applicationAssembly);
    }

    private sealed record DeviceConnectionSettings
    {
        public string RabbitConnectionString { get; init; } = string.Empty;

        public string WriteConnectionString { get; init; } = string.Empty;

        public string ReadConnectionString { get; init; } = string.Empty;

        public DatabaseProvider DatabaseProvider { get; init; } = default!;
    }
}

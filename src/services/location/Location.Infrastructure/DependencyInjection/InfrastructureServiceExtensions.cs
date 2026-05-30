// <copyright file="InfrastructureServiceExtensions.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Reflection;
using Location.Application.Service.Abstractions;
using Location.Infrastructure.Persistence;
using Location.Infrastructure.Persistence.Repositories.Read;
using Location.Infrastructure.Persistence.Repositories.Write;
using Location.Infrastructure.Service;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using SharedKernel.Core.Exceptions;
using SharedKernel.Core.Licensing;
using SharedKernel.Core.Pricing;
using SharedKernel.Infrastructure;
using SharedKernel.Persistence.Database;

namespace Location.Infrastructure.DependencyInjection;

/// <summary>
/// Registers infrastructure services for Location.
/// </summary>
public static class InfrastructureServiceExtensions
{
    /// <summary>
    /// Adds Location infrastructure dependencies.
    /// </summary>
    /// <param name="builder">Application builder.</param>
    /// <param name="applicationAssembly">Application assembly.</param>
    public static void AddInfrastructureServices(this WebApplicationBuilder builder, Assembly applicationAssembly)
    {
        ValidateInputs(builder, applicationAssembly);
        bool isRunningGeneration = CodeGenerationDetector.IsRunningGeneration();
        LocationConnectionSettings connectionSettings = ResolveConnectionSettings(builder.Configuration, isRunningGeneration);

        ConfigureDatabase(builder, connectionSettings);
        RegisterServices(builder.Services);
        ConfigureLicensing(builder);

        if (!isRunningGeneration)
        {
            builder.Services.AddHostedService<TemplateFontMetadataInitializationService>();
        }
    }

    /// <summary>
    /// Adds Location infrastructure middleware.
    /// </summary>
    /// <param name="app">Application builder.</param>
    /// <returns>Configured application builder.</returns>
    public static IApplicationBuilder UseInfrastructureServices(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.Use(async (context, next) =>
        {
            if (IsMultipartWithoutBoundary(context.Request))
            {
                string traceId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;

                ProblemDetails problem = new()
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Invalid Request Payload",
                    Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                    Detail = "Multipart form-data request is malformed.",
                };

                problem.Extensions["traceId"] = traceId;
                problem.Extensions["errors"] = new[]
                {
                    new
                    {
                        name = "request.malformedMultipart",
                        reason = "The multipart/form-data request is missing a valid boundary.",
                    },
                };

                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                context.Response.ContentType = "application/problem+json";
                await context.Response.WriteAsJsonAsync(problem).ConfigureAwait(false);
                return;
            }

            await next().ConfigureAwait(false);
        });

        return app;
    }

    private static bool IsMultipartWithoutBoundary(HttpRequest request)
    {
        string? contentType = request.ContentType;
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return false;
        }

        if (!contentType.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return contentType.IndexOf("boundary=", StringComparison.OrdinalIgnoreCase) < 0;
    }

    private static void ConfigureDatabase(WebApplicationBuilder builder, LocationConnectionSettings settings)
    {
        Assembly? migrationsAssembly = null;

        builder.AddCqrsDatabase(
            migrationsAssembly,
            settings.WriteConnectionString,
            settings.ReadConnectionString,
            settings.DatabaseProvider);
    }

    private static void RegisterServices(IServiceCollection services)
    {
        services.AddScoped<IDisplayModelReadRepository, DbDisplayModelReadRepository>();
        services.AddScoped<ILocationNodeReadRepository, DbLocationNodeReadRepository>();
        services.AddScoped<ILocationNodeWriteRepository, DbLocationNodeWriteRepository>();
        services.AddScoped<ITemplateDesignReadRepository, DbTemplateDesignReadRepository>();
        services.AddScoped<ITemplateDesignWriteRepository, DbTemplateDesignWriteRepository>();
        services.AddScoped<ITemplateScopeSettingsReadRepository, DbTemplateScopeSettingsReadRepository>();
        services.AddScoped<ITemplateScopeSettingsWriteRepository, DbTemplateScopeSettingsWriteRepository>();
        services.AddScoped<ILocationGroupReadRepository, DbLocationGroupReadRepository>();
        services.AddScoped<ILocationGroupWriteRepository, DbLocationGroupWriteRepository>();
        services.AddScoped<ITemplateInheritanceResolver, TemplateInheritanceResolver>();
        services.AddSingleton<ITemplateFontAssetService, TemplateFontAssetService>();
    }

    private static void ConfigureLicensing(WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<ILicenseValidator, Licensing.LocationLicenseValidator>();
    }

    private static LocationConnectionSettings ResolveConnectionSettings(IConfiguration configuration, bool isRunningGeneration)
    {
        DatabaseProvider databaseProvider = configuration.GetDatabaseProvider();
        if (isRunningGeneration)
        {
            return CreateCodeGenerationConnectionSettings(databaseProvider);
        }

        return new LocationConnectionSettings
        {
            WriteConnectionString = ResolveWriteConnectionString(configuration, databaseProvider),
            ReadConnectionString = ResolveReadConnectionString(configuration, databaseProvider),
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

    private static LocationConnectionSettings CreateCodeGenerationConnectionSettings(DatabaseProvider provider)
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

        return new LocationConnectionSettings
        {
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

    private sealed class TemplateFontMetadataInitializationService(
            IDbContextFactory<TemplateFontMetadataDbContext> dbContextFactory)
            : IHostedService
        {
            public async Task StartAsync(CancellationToken cancellationToken)
            {
                await using TemplateFontMetadataDbContext dbContext = await dbContextFactory
                    .CreateDbContextAsync(cancellationToken)
                    .ConfigureAwait(false);

                await SeedDisplayModelsAsync(dbContext, cancellationToken).ConfigureAwait(false);
            }

            public Task StopAsync(CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }

            private static async Task SeedDisplayModelsAsync(
                TemplateFontMetadataDbContext dbContext,
                CancellationToken cancellationToken)
            {
                bool hasSharedModels = await dbContext.DisplayModels
                    .AnyAsync(model => model.TenantId == DisplayModelSeedData.SharedTenantId, cancellationToken)
                    .ConfigureAwait(false);

                if (hasSharedModels)
                {
                    return;
                }

                DateTimeOffset updatedAtUtc = DateTimeOffset.UtcNow;

                foreach (DisplayModelSeedItem model in DisplayModelSeedData.SharedDefaults)
                {
                    dbContext.DisplayModels.Add(new DisplayModelRecord
                    {
                        Id = Guid.NewGuid(),
                        TenantId = DisplayModelSeedData.SharedTenantId,
                        DisplayModelId = model.DisplayModelId,
                        Name = model.Name,
                        Width = model.Width,
                        Height = model.Height,
                        UpdatedAtUtc = updatedAtUtc,
                    });
                }

                await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
        }

    private sealed record LocationConnectionSettings
    {
        public string WriteConnectionString { get; init; } = string.Empty;

        public string ReadConnectionString { get; init; } = string.Empty;

        public DatabaseProvider DatabaseProvider { get; init; } = default!;
    }
}

using System.Reflection;
using Finbuckle.MultiTenant.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel.Core.Caching;
using SharedKernel.Core.Pricing;
using SharedKernel.Infrastructure.HealthChecks;
using SharedKernel.Infrastructure.MultiTenant;
using SharedKernel.Persistence.Caching;
using SharedKernel.Persistence.Database.EFCore;
using SharedKernel.Persistence.Database.EFCore.Interceptors;

namespace SharedKernel.Persistence.Database.MultiTenant;

/// <summary>
/// Extensions for configuring hybrid multi-tenant database support.
/// </summary>
public static class MultiTenantDbExtensions
{
    /// <summary>
    /// Adds hybrid multi-tenant database contexts that support both shared and isolated databases.
    /// </summary>
    /// <remarks>
    /// Use this method for services that need multi-tenancy capabilities (like Site.Api, Device.Api, Catalog.Api).
    /// This approach supports runtime switching between shared database, dedicated databases, or external databases
    /// based on tenant configuration.
    /// For services that don't need multi-tenancy (like Customer.Api), use AddCustomDbContexts from Extensions.cs instead.
    /// </remarks>
    /// <typeparam name="TWriteContext">The write context type.</typeparam>
    /// <typeparam name="TReadContext">The read context type.</typeparam>
    /// <param name="builder">The web application builder.</param>
    /// <param name="migrationsAssembly">The assembly containing migrations.</param>
    /// <param name="defaultWriteConnectionString">The default write connection string for shared database.</param>
    /// <param name="defaultReadConnectionString">The default read connection string for shared database.</param>
    /// <param name="defaultProvider">The default database provider (defaults to PostgreSQL).</param>
    public static void AddHybridMultiTenantDbContexts<TWriteContext, TReadContext>(
        this WebApplicationBuilder builder,
        Assembly migrationsAssembly,
        string defaultWriteConnectionString,
        string defaultReadConnectionString,
        DatabaseProvider? defaultProvider = null)
        where TWriteContext : BaseDbContext
        where TReadContext : BaseDbContext
    {
        DatabaseProvider effectiveProvider = defaultProvider ?? DatabaseProvider.PostgreSQL;

        builder.Services.AddScoped(typeof(IGenericCacheService<,>), typeof(GenericCacheService<,>));

        // Register the tenant database resolver service
        builder.Services.AddScoped<ITenantDbConnectionResolver>(sp =>
        {
            return new TenantDbConnectionResolver(sp, defaultWriteConnectionString, defaultReadConnectionString, effectiveProvider);
        });
        builder.Services.AddScoped<AuditingInterceptor>();
        builder.Services.AddScoped<SoftDeleteInterceptor>();

        // Register the custom provider for tenant-aware DbContext access
        builder.Services.AddScoped(typeof(ICurrentTenantDbContext<>), typeof(CurrentTenantDbContext<>));

        // Register HTTP client for Customer.Api
        builder.Services.AddHttpClient("CustomerApi", client =>
        {
            // Configure base address from configuration if available
            var customerApiUrl = builder.Configuration["Services:CustomerApi:Url"];
            if (!string.IsNullOrEmpty(customerApiUrl))
            {
                client.BaseAddress = new Uri(customerApiUrl);
            }

            // Add default headers, timeout, etc.
            client.Timeout = TimeSpan.FromSeconds(5);
        });

        // Register context factories for use with ICurrentTenantDbContext and other consumers
        // Use AddDbContextFactory with explicit options configuration to avoid constructor ambiguity
        builder.Services.AddDbContextFactory<TWriteContext>((serviceProvider, options) =>
        {
            // Configure the factory to use basic DbContext options - tenant resolution happens at runtime
            ConfigureTenantDbContext(
                options,
                serviceProvider,
                defaultWriteConnectionString,
                migrationsAssembly,
                effectiveProvider,
                DatabaseStrategy.Shared,
                false);
        });

        builder.Services.AddDbContextFactory<TReadContext>((serviceProvider, options) =>
        {
            // Configure the factory to use basic DbContext options - tenant resolution happens at runtime
            ConfigureTenantDbContext(
                options,
                serviceProvider,
                defaultReadConnectionString,
                migrationsAssembly: null,
                effectiveProvider,
                DatabaseStrategy.Shared,
                true);
        });

        // Register single-parameter constructor for EF tooling and MassTransit outbox
        builder.Services.AddDbContext<TWriteContext>((sp, options) =>
        {
            // Use the default write connection string and provider for tooling/outbox
            ConfigureTenantDbContext(
                options,
                null,
                defaultWriteConnectionString,
                migrationsAssembly,
                effectiveProvider,
                DatabaseStrategy.Shared,
                isReadOnly: false);
        });

        // Runtime-tenant-aware write context registration
        builder.Services.AddScoped<TWriteContext>(sp =>
        {
            var tenantAccessor = sp.GetRequiredService<IMultiTenantContextAccessor<TenantDetails>>();
            var tenantInfo = tenantAccessor.MultiTenantContext?.TenantInfo;
            var connectionResolver = sp.GetRequiredService<ITenantDbConnectionResolver>();
            (string WriteConnectionString, string? ReadConnectionString, DatabaseProvider Provider, DatabaseStrategy Strategy) resolved;
            if (tenantInfo != null)
            {
                resolved = connectionResolver.ResolveTenantConnection(tenantInfo);
            }
            else
            {
                resolved = (defaultWriteConnectionString, defaultReadConnectionString, effectiveProvider, DatabaseStrategy.Shared);
            }

            var optionsBuilder = new DbContextOptionsBuilder<TWriteContext>();
            DatabaseProvider effectiveResolvedProvider = resolved.Provider ?? effectiveProvider;
            DatabaseStrategy effectiveResolvedStrategy = resolved.Strategy ?? DatabaseStrategy.Shared;
            ConfigureTenantDbContext(
                optionsBuilder,
                sp,
                resolved.WriteConnectionString,
                migrationsAssembly,
                effectiveResolvedProvider,
                effectiveResolvedStrategy,
                isReadOnly: false);

            // Use single constructor - tenant info is embedded in the options
            return ActivatorUtilities.CreateInstance<TWriteContext>(sp, optionsBuilder.Options);
        });

        // Runtime-tenant-aware read context registration
        builder.Services.AddScoped<TReadContext>(sp =>
        {
            var tenantAccessor = sp.GetRequiredService<IMultiTenantContextAccessor<TenantDetails>>();
            var tenantInfo = tenantAccessor.MultiTenantContext?.TenantInfo;
            var connectionResolver = sp.GetRequiredService<ITenantDbConnectionResolver>();
            (string WriteConnectionString, string? ReadConnectionString, DatabaseProvider Provider, DatabaseStrategy Strategy) resolved;
            if (tenantInfo != null)
            {
                resolved = connectionResolver.ResolveTenantConnection(tenantInfo);
            }
            else
            {
                resolved = (defaultWriteConnectionString, defaultReadConnectionString, effectiveProvider, DatabaseStrategy.Shared);
            }

            var optionsBuilder = new DbContextOptionsBuilder<TReadContext>();
            DatabaseProvider effectiveResolvedProvider = resolved.Provider ?? effectiveProvider;
            DatabaseStrategy effectiveResolvedStrategy = resolved.Strategy ?? DatabaseStrategy.Shared;
            ConfigureTenantDbContext(
                optionsBuilder,
                sp,
                resolved.ReadConnectionString ?? resolved.WriteConnectionString,
                migrationsAssembly: null,
                effectiveResolvedProvider,
                effectiveResolvedStrategy,
                isReadOnly: true);

            // Use single constructor - tenant info is embedded in the options
            return ActivatorUtilities.CreateInstance<TReadContext>(sp, optionsBuilder.Options);
        });

        // Do NOT register TWriteContext or TReadContext directly for DI to avoid accidental direct injection and constructor errors
        // Only register the factories and context interfaces as needed

        // Add provider-specific read/write database health checks for readiness
        builder.AddReadWriteHealthChecks(effectiveProvider, defaultWriteConnectionString, defaultReadConnectionString);
    }

    /// <summary>
    /// Configures a tenant-specific database context.
    /// </summary>
    private static void ConfigureTenantDbContext(
        DbContextOptionsBuilder options,
        IServiceProvider? serviceProvider,
        string connectionString,
        Assembly? migrationsAssembly,
        DatabaseProvider provider,
        DatabaseStrategy strategy,
        bool isReadOnly)
    {
        provider ??= DatabaseProvider.PostgreSQL;
        strategy ??= DatabaseStrategy.Shared;

        // Configure provider-specific options using SmartEnum Name
        if (provider.Name == DatabaseProvider.PostgreSQL.Name)
        {
            if (migrationsAssembly != null)
            {
                options.UseNpgsql(connectionString, npgsql =>
                    npgsql.MigrationsAssembly(migrationsAssembly.FullName));
            }
            else
            {
                options.UseNpgsql(connectionString);
            }
        }
        else if (provider.Name == DatabaseProvider.SqlServer.Name)
        {
            if (migrationsAssembly != null)
            {
                options.UseSqlServer(connectionString, sqlServer =>
                    sqlServer.MigrationsAssembly(migrationsAssembly.FullName));
            }
            else
            {
                options.UseSqlServer(connectionString);
            }
        }
        else if (provider.Name == DatabaseProvider.MySQL.Name)
        {
            if (migrationsAssembly != null)
            {
                options.UseMySQL(
                    connectionString,
                    mysql => mysql.MigrationsAssembly(migrationsAssembly.FullName));
            }
            else
            {
                options.UseMySQL(
                    connectionString);
            }
        }
        else
        {
            throw new ArgumentException($"Unsupported database provider: {provider}");
        }

        if (migrationsAssembly != null)
        {
            options.ReplaceService<IMigrationsAssembly, ProviderFilteredMigrationsAssembly>();
        }

        // Add required interceptors only when we have a valid service provider
        // (not during factory registration time)
        if (serviceProvider != null)
        {
            if (isReadOnly)
            {
                // For read-only context, add only auditing
                var auditingInterceptor = serviceProvider.GetRequiredService<AuditingInterceptor>();
                options.AddInterceptors(auditingInterceptor);

                // Optimize for reading
                options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
            }
            else
            {
                // For write context, add all interceptors
                var softDeleteInterceptor = serviceProvider.GetRequiredService<SoftDeleteInterceptor>();
                var auditingInterceptor = serviceProvider.GetRequiredService<AuditingInterceptor>();

                options.AddInterceptors(
                    softDeleteInterceptor,
                    auditingInterceptor);
            }
        }
        else
        {
            // During factory registration, just configure basic query behavior
            if (isReadOnly)
            {
                options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
            }
        }

        // For shared database strategy, configure Finbuckle multi-tenancy
        // This is only needed for the shared database option
        if (strategy == DatabaseStrategy.Shared)
        {
            // The multi-tenant configuration is applied in the DbContext's OnModelCreating method
            // instead of here, using modelBuilder.ConfigureMultiTenant()
        }
    }
}

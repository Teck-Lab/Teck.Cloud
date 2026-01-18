using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SharedKernel.Core.Pricing;
using SharedKernel.Persistence.Database.EFCore;
using SharedKernel.Persistence.Database.EFCore.Interceptors;

namespace SharedKernel.Persistence.Database
{
    /// <summary>
    /// Database extensions for registering EF Core contexts and related services.
    /// </summary>
    public static class Extensions
    {
        /// <summary>
        /// Adds both read and write database contexts to the application with the specified provider.
        /// </summary>
        /// <remarks>
        /// Use this method for services that don't need multi-tenancy (like Customer.Api).
        /// For multi-tenant services, use AddHybridMultiTenantDbContexts from MultiTenantDbExtensions instead.
        /// </remarks>
        /// <typeparam name="TWriteContext">The write context type (for commands).</typeparam>
        /// <typeparam name="TReadContext">The read context type (for queries).</typeparam>
        /// <param name="builder">The web application builder.</param>
        /// <param name="assembly">The assembly containing migrations.</param>
        /// <param name="defaultWriteConnectionString"></param>
        /// <param name="defaultReadConnectionString"></param>
        /// <param name="provider">The database provider to use (defaults to PostgreSQL).</param>
        public static void AddCustomDbContexts<TWriteContext, TReadContext>(
            this WebApplicationBuilder builder,
            Assembly assembly,
            string defaultWriteConnectionString,
            string defaultReadConnectionString,
            DatabaseProvider provider)
            where TWriteContext : BaseDbContext
            where TReadContext : BaseDbContext
        {
            // Defensive: Prevent accidental registration of multi-tenant contexts
            var forbidden = new[] { "ApplicationWriteDbContext", "ApplicationReadDbContext" };
            if (forbidden.Contains(typeof(TWriteContext).Name) || forbidden.Contains(typeof(TReadContext).Name))
                throw new InvalidOperationException($"Do not use AddCustomDbContexts for multi-tenant contexts like {typeof(TWriteContext).Name} or {typeof(TReadContext).Name}. Use AddHybridMultiTenantDbContexts instead.");

            // Add write context with all interceptors
            AddWriteDbContext<TWriteContext>(builder, assembly, defaultWriteConnectionString, provider);

            // Add read context with minimal interceptors
            AddReadDbContext<TReadContext>(builder, defaultReadConnectionString, provider);

            // Add health check for the appropriate provider
            AddDbHealthCheck(builder, defaultWriteConnectionString, defaultReadConnectionString, provider);
        }

        /// <summary>
        /// Adds a write database context with all required interceptors.
        /// </summary>
        private static void AddWriteDbContext<TContext>(
            WebApplicationBuilder builder,
            Assembly assembly,
            string connectionString,
            DatabaseProvider provider)
            where TContext : BaseDbContext
        {
            // Defensive: Prevent accidental registration of multi-tenant contexts
            var forbidden = new[] { "ApplicationWriteDbContext", "ApplicationReadDbContext" };
            if (forbidden.Contains(typeof(TContext).Name))
                throw new InvalidOperationException($"Do not use AddWriteDbContext for multi-tenant context {typeof(TContext).Name}. Use AddHybridMultiTenantDbContexts instead.");
            builder.Services.AddScoped<SoftDeleteInterceptor>();
            builder.Services.AddScoped<AuditingInterceptor>();

            builder.Services.AddDbContext<TContext>((sp, options) =>
            {
                ConfigureDbContextOptions(options, connectionString, assembly, provider);

                options.AddInterceptors(
                    sp.GetRequiredService<SoftDeleteInterceptor>(),
                    sp.GetRequiredService<AuditingInterceptor>());
            });

            // Enrich the context based on the provider
            EnrichDbContext<TContext>(builder, provider);
            builder.Services.AddScoped<TContext>();
            builder.Services.AddScoped<IBaseDbContext>(sp => (IBaseDbContext)sp.GetRequiredService<TContext>());
        }

        /// <summary>
        /// Adds a read-only database context with minimal interceptors.
        /// </summary>
        private static void AddReadDbContext<TContext>(
            WebApplicationBuilder builder,
            string connectionString,
            DatabaseProvider provider)
            where TContext : BaseDbContext
        {
            // Defensive: Prevent accidental registration of multi-tenant contexts
            var forbidden = new[] { "ApplicationWriteDbContext", "ApplicationReadDbContext" };
            if (forbidden.Contains(typeof(TContext).Name))
                throw new InvalidOperationException($"Do not use AddReadDbContext for multi-tenant context {typeof(TContext).Name}. Use AddHybridMultiTenantDbContexts instead.");
            builder.Services.AddScoped<AuditingInterceptor>();

            builder.Services.AddDbContext<TContext>((sp, options) =>
            {
                ConfigureDbContextOptions(options, connectionString, null, provider);

                options.AddInterceptors(
                    sp.GetRequiredService<AuditingInterceptor>());

                // Enable read-optimized features in EF Core
                options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
            });

            // Enrich the context based on the provider
            EnrichDbContext<TContext>(builder, provider);
            builder.Services.AddScoped<TContext>();

            // Register as IBaseDbContext so it can be used for read operations
            builder.Services.AddScoped<IBaseDbContext>(sp => (IBaseDbContext)sp.GetRequiredService<TContext>());
        }

        /// <summary>
        /// Configures database context options based on the selected provider.
        /// </summary>
        private static void ConfigureDbContextOptions(
            DbContextOptionsBuilder options,
            string connectionString,
            Assembly? migrationsAssembly,
            DatabaseProvider provider)
        {
            if (provider == DatabaseProvider.PostgreSQL)
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
            else if (provider == DatabaseProvider.SqlServer)
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
            else if (provider == DatabaseProvider.MySQL)
            {
                if (migrationsAssembly != null)
                {
                    options.UseMySQL(
                        connectionString,
                        mysql => mysql.MigrationsAssembly(migrationsAssembly.FullName));
                }
                else
                {
                    options.UseMySQL(connectionString);
                }
            }
            else
            {
                throw new ArgumentException($"Unsupported database provider: {provider}");
            }
        }

        /// <summary>
        /// Enriches the DbContext with provider-specific settings.
        /// </summary>
        private static void EnrichDbContext<TContext>(
            WebApplicationBuilder builder,
            DatabaseProvider provider)
            where TContext : BaseDbContext
        {
            if (provider == DatabaseProvider.PostgreSQL)
            {
                builder.EnrichNpgsqlDbContext<TContext>();
            }
            else if (provider == DatabaseProvider.MySQL)
            {
                // Add any MySQL specific enrichment here if needed
            }
            else if (provider == DatabaseProvider.SqlServer)
            {
                // Add any SQL Server specific enrichment here
                // For now, we don't have any SQL Server specific enrichment
            }
            else
            {
                throw new ArgumentException($"Unsupported database provider: {provider}");
            }
        }

        /// <summary>
        /// Adds appropriate health checks based on the database provider.
        /// </summary>
        private static void AddDbHealthCheck(
            WebApplicationBuilder builder,
            string defaultWriteConnectionString,
            string defaultReadConnectionString,
            DatabaseProvider provider)
        {
            var healthChecks = builder.Services.AddHealthChecks();

            // If read and write connection strings are the same, only add one health check (for write)
            if (defaultWriteConnectionString == defaultReadConnectionString)
            {
                if (provider == DatabaseProvider.PostgreSQL)
                {
                    healthChecks.AddNpgSql(
                        connectionString: defaultWriteConnectionString,
                        tags: ["db", "sql", "postgres", "write"]);
                }
                else if (provider == DatabaseProvider.MySQL)
                {
                    healthChecks.AddMySql(
                        connectionString: defaultWriteConnectionString,
                        tags: ["db", "sql", "mysql", "write"]);
                }
                else if (provider == DatabaseProvider.SqlServer)
                {
                    healthChecks.AddSqlServer(
                        connectionString: defaultWriteConnectionString,
                        tags: ["db", "sql", "sqlserver", "write"]);
                }
                else
                {
                    throw new ArgumentException($"Unsupported database provider: {provider}");
                }
            }
            else
            {
                // Add health check for write database
                if (provider == DatabaseProvider.PostgreSQL)
                {
                    healthChecks.AddNpgSql(
                        connectionString: defaultWriteConnectionString,
                        tags: ["db", "sql", "postgres", "write"]);
                }
                else if (provider == DatabaseProvider.MySQL)
                {
                    healthChecks.AddMySql(
                        connectionString: defaultWriteConnectionString,
                        tags: ["db", "sql", "mysql", "write"]);
                }
                else if (provider == DatabaseProvider.SqlServer)
                {
                    healthChecks.AddSqlServer(
                        connectionString: defaultWriteConnectionString,
                        tags: ["db", "sql", "sqlserver", "write"]);
                }
                else
                {
                    throw new ArgumentException($"Unsupported database provider: {provider}");
                }

                // Add health check for read database
                if (provider == DatabaseProvider.PostgreSQL)
                {
                    healthChecks.AddNpgSql(
                        connectionString: defaultReadConnectionString,
                        tags: ["db", "sql", "postgres", "read"]);
                }
                else if (provider == DatabaseProvider.MySQL)
                {
                    healthChecks.AddMySql(
                        connectionString: defaultReadConnectionString,
                        tags: ["db", "sql", "mysql", "read"]);
                }
                else if (provider == DatabaseProvider.SqlServer)
                {
                    healthChecks.AddSqlServer(
                        connectionString: defaultReadConnectionString,
                        tags: ["db", "sql", "sqlserver", "read"]);
                }
                else
                {
                    throw new ArgumentException($"Unsupported database provider: {provider}");
                }
            }
        }

        /// <summary>
        /// Gets the database provider from configuration.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        /// <param name="connectionName">The connection string name (defaults to "defaultdb").</param>
        /// <returns>The database provider.</returns>
        public static DatabaseProvider GetDatabaseProvider(this IConfiguration configuration, string connectionName = "defaultdb")
        {
            var providerName = configuration.GetConnectionString($"{connectionName}:DbProvider")
                ?? configuration[$"ConnectionStrings:{connectionName}:DbProvider"]
                ?? "PostgreSQL";

            return providerName.ToLowerInvariant() switch
            {
                "sqlserver" => DatabaseProvider.SqlServer,
                "mysql" => DatabaseProvider.MySQL,
                _ => DatabaseProvider.PostgreSQL // Default to PostgreSQL
            };
        }
    }
}

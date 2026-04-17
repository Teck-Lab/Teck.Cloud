using Catalog.Infrastructure.Persistence;
using Customer.Infrastructure.Persistence;
using JasperFx;
using JasperFx.Resources;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using SharedKernel.Core.Pricing;
using SharedKernel.Persistence.Database.MultiTenant;
using Wolverine;
using Wolverine.Postgresql;
using Wolverine.SqlServer;

string service = string.Empty;
string mode = "shared";

for (int argIndex = 0; argIndex < args.Length - 1; argIndex++)
{
    if (string.Equals(args[argIndex], "--service", StringComparison.OrdinalIgnoreCase))
    {
        service = args[argIndex + 1];
    }

    if (string.Equals(args[argIndex], "--mode", StringComparison.OrdinalIgnoreCase))
    {
        mode = args[argIndex + 1];
    }
}

if (string.IsNullOrWhiteSpace(service))
{
    await Console.Error.WriteLineAsync("ERROR: --service <catalog|customer> argument is required.");
    return 1;
}

if (mode is not ("shared" or "dedicated" or "all"))
{
    await Console.Error.WriteLineAsync("ERROR: --mode must be one of: shared, dedicated, all.");
    return 1;
}

DatabaseProvider provider = ResolveProvider(Environment.GetEnvironmentVariable("DATABASE_PROVIDER"));

// ---- Shared migration (skipped for --mode dedicated) ----
if (mode is "shared" or "all")
{
    string? connectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING");
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        await Console.Error.WriteLineAsync("ERROR: CONNECTION_STRING environment variable is required.");
        return 1;
    }

    if (provider == DatabaseProvider.PostgreSQL &&
        !connectionString.Contains("Search Path", StringComparison.OrdinalIgnoreCase) &&
        !connectionString.Contains("SearchPath", StringComparison.OrdinalIgnoreCase))
    {
        connectionString = connectionString.TrimEnd(';') + ";Search Path=public";
    }

    IHostBuilder hostBuilder = service.ToLowerInvariant() switch
    {
        "catalog" => CreateCatalogHost(connectionString, provider),
        "customer" => CreateCustomerHost(connectionString, provider),
        _ => throw new InvalidOperationException(
            $"Unknown service '{service}'. Valid values: catalog, customer.")
    };

    IHost host = hostBuilder.Build();
    ILogger sharedLogger = host.Services.GetRequiredService<ILogger<Program>>();

    try
    {
        sharedLogger.LogInformation(
            "Starting shared migration for service {Service} using provider {Provider}",
            service,
            provider.Name);

        await ApplyEfCoreMigrationsAsync(host, service, sharedLogger);

        if (provider != DatabaseProvider.MySQL)
        {
            await host.SetupResources(CancellationToken.None);
        }

        sharedLogger.LogInformation("Shared migration for service {Service} completed", service);
    }
    catch (Exception migrationException)
    {
        sharedLogger.LogError(migrationException, "Shared migration for service {Service} failed", service);
        return 1;
    }
}

// ---- Dedicated-tenant migrations (--mode dedicated or all) ----
if (mode is "dedicated" or "all")
{
    using var loggerFactory = LoggerFactory.Create(b => b.AddConsole().SetMinimumLevel(LogLevel.Information));
    var dedicatedLogger = loggerFactory.CreateLogger("Migrations.Dedicated");

    string? customerConnectionString = Environment.GetEnvironmentVariable("CUSTOMER_CONNECTION_STRING");
    if (string.IsNullOrWhiteSpace(customerConnectionString))
    {
        await Console.Error.WriteLineAsync(
            "ERROR: CUSTOMER_CONNECTION_STRING environment variable is required for dedicated migrations.");
        return 1;
    }

    var openBaoOptions = new OpenBaoOptions
    {
        Url = Environment.GetEnvironmentVariable("OpenBao__Url") ?? string.Empty,
        Token = Environment.GetEnvironmentVariable("OpenBao__Token"),
        AuthPath = Environment.GetEnvironmentVariable("OpenBao__AuthPath") ?? "kubernetes",
        Role = Environment.GetEnvironmentVariable("OpenBao__Role") ?? string.Empty,
        KvMount = Environment.GetEnvironmentVariable("OpenBao__KvMount") ?? "teck-cloud",
    };

    if (string.IsNullOrWhiteSpace(openBaoOptions.Url))
    {
        await Console.Error.WriteLineAsync(
            "ERROR: OpenBao__Url environment variable is required for dedicated migrations.");
        return 1;
    }

    var vaultLogger = loggerFactory.CreateLogger<VaultTenantConnectionProvider>();
    var vaultProvider = new VaultTenantConnectionProvider(openBaoOptions, service, vaultLogger);

    int failedCount = await MigrateDedicatedTenantsAsync(
        service, vaultProvider, customerConnectionString, dedicatedLogger);

    if (failedCount > 0)
    {
        dedicatedLogger.LogError(
            "{FailedCount} dedicated-tenant migration(s) failed for service {Service}",
            failedCount,
            service);
        return 1;
    }
}

return 0;

/// <summary>
/// Resolves the database provider from the DATABASE_PROVIDER environment variable.
/// Accepts the same values as the Database__Provider configuration key used by service hosts.
/// Defaults to PostgreSQL when the variable is absent or unrecognised.
/// </summary>
static DatabaseProvider ResolveProvider(string? envValue) =>
    envValue?.Trim().ToLowerInvariant() switch
    {
        "sqlserver" or "mssql" => DatabaseProvider.SqlServer,
        "mysql" or "mariadb" => DatabaseProvider.MySQL,
        _ => DatabaseProvider.PostgreSQL
    };

/// <summary>
/// Returns the EF Core migrations assembly name for the given service prefix and provider.
/// </summary>
static string ResolveMigrationsAssembly(string servicePrefix, DatabaseProvider provider) =>
    provider.Name switch
    {
        "SqlServer" => $"{servicePrefix}.Infrastructure.Migrations.SqlServer",
        "MySQL" => $"{servicePrefix}.Infrastructure.Migrations.MySql",
        _ => $"{servicePrefix}.Infrastructure.Migrations.PostgreSQL"
    };

/// <summary>
/// Applies EF Core provider-specific options to a <see cref="DbContextOptionsBuilder"/>.
/// </summary>
static void ConfigureDbContextOptions(
    DbContextOptionsBuilder options,
    string connectionString,
    string migrationsAssembly,
    DatabaseProvider provider)
{
    if (provider == DatabaseProvider.PostgreSQL)
    {
        options.UseNpgsql(connectionString, npgsql => npgsql.MigrationsAssembly(migrationsAssembly));
    }
    else if (provider == DatabaseProvider.SqlServer)
    {
        options.UseSqlServer(connectionString, sqlServer => sqlServer.MigrationsAssembly(migrationsAssembly));
    }
    else if (provider == DatabaseProvider.MySQL)
    {
        options.UseMySQL(connectionString, mysql => mysql.MigrationsAssembly(migrationsAssembly));
    }
}

/// <summary>
/// Configures Wolverine durable message persistence for providers that support it.
/// MySQL is intentionally skipped because WolverineFx does not ship a MySQL transport.
/// </summary>
static void ConfigureWolverinePersistence(WolverineOptions opts, string connectionString, DatabaseProvider provider)
{
    if (provider == DatabaseProvider.PostgreSQL)
    {
        opts.PersistMessagesWithPostgresql(connectionString, "wolverine")
            .OverrideAutoCreateResources(AutoCreate.CreateOrUpdate);
    }
    else if (provider == DatabaseProvider.SqlServer)
    {
        opts.PersistMessagesWithSqlServer(connectionString, "wolverine")
            .OverrideAutoCreateResources(AutoCreate.CreateOrUpdate);
    }
}

static async Task ApplyEfCoreMigrationsAsync(IHost host, string service, ILogger logger)
{
    await using AsyncServiceScope scope = host.Services.CreateAsyncScope();

    DbContext dbContext = service.ToLowerInvariant() switch
    {
        "catalog" => scope.ServiceProvider.GetRequiredService<ApplicationWriteDbContext>(),
        "customer" => scope.ServiceProvider.GetRequiredService<CustomerWriteDbContext>(),
        _ => throw new InvalidOperationException(
            $"No DbContext registered for service '{service}'.")
    };

    IReadOnlyList<string> pending = (await dbContext.Database.GetPendingMigrationsAsync()).ToList();

    if (pending.Count == 0)
    {
        logger.LogInformation("No pending EF Core migrations for {Service}", service);
        return;
    }

    logger.LogInformation(
        "Applying {Count} pending EF Core migration(s) for {Service}: {Migrations}",
        pending.Count,
        service,
        string.Join(", ", pending));

    await dbContext.Database.MigrateAsync();

    logger.LogInformation("EF Core migrations applied for {Service}", service);
}

static IHostBuilder CreateCatalogHost(string connectionString, DatabaseProvider provider)
{
    string migrationsAssembly = ResolveMigrationsAssembly("Catalog", provider);

    IHostBuilder builder = Host.CreateDefaultBuilder()
        .ConfigureServices(services =>
        {
            services.AddDbContext<ApplicationWriteDbContext>(
                options => ConfigureDbContextOptions(options, connectionString, migrationsAssembly, provider),
                optionsLifetime: ServiceLifetime.Singleton);
        });

    if (provider != DatabaseProvider.MySQL)
    {
        builder = builder.UseWolverine(opts =>
        {
            ConfigureWolverinePersistence(opts, connectionString, provider);
        });
    }

    return builder;
}

static IHostBuilder CreateCustomerHost(string connectionString, DatabaseProvider provider)
{
    string migrationsAssembly = ResolveMigrationsAssembly("Customer", provider);

    IHostBuilder builder = Host.CreateDefaultBuilder()
        .ConfigureServices(services =>
        {
            services.AddDbContext<CustomerWriteDbContext>(
                options => ConfigureDbContextOptions(options, connectionString, migrationsAssembly, provider),
                optionsLifetime: ServiceLifetime.Singleton);
        });

    if (provider != DatabaseProvider.MySQL)
    {
        builder = builder.UseWolverine(opts =>
        {
            ConfigureWolverinePersistence(opts, connectionString, provider);
        });
    }

    return builder;
}

/// <summary>
/// Runs EF Core migrations for every active dedicated tenant that has a
/// database entry for the given service. Vault is queried for each tenant's
/// connection string; failures are logged but do not abort remaining tenants.
/// </summary>
static async Task<int> MigrateDedicatedTenantsAsync(
    string service,
    VaultTenantConnectionProvider vaultProvider,
    string customerConnectionString,
    ILogger logger,
    CancellationToken ct = default)
{
    IReadOnlyList<(string Identifier, string Provider)> tenants =
        await DiscoverDedicatedTenantsAsync(customerConnectionString, service, logger, ct);

    if (tenants.Count == 0)
    {
        logger.LogInformation("No dedicated tenants found for service {Service}", service);
        return 0;
    }

    logger.LogInformation(
        "Found {Count} dedicated tenant(s) for service {Service}: {Tenants}",
        tenants.Count,
        service,
        string.Join(", ", tenants.Select(t => t.Identifier)));

    int failedCount = 0;

    foreach (var (identifier, tenantProviderName) in tenants)
    {
        try
        {
            logger.LogInformation(
                "Migrating dedicated tenant {TenantIdentifier} for service {Service}",
                identifier,
                service);

            var (writeConnectionString, _) = await vaultProvider.GetAsync(identifier, ct);

            DatabaseProvider tenantProvider = ResolveProvider(tenantProviderName);

            if (tenantProvider == DatabaseProvider.PostgreSQL &&
                !writeConnectionString.Contains("Search Path", StringComparison.OrdinalIgnoreCase) &&
                !writeConnectionString.Contains("SearchPath", StringComparison.OrdinalIgnoreCase))
            {
                writeConnectionString = writeConnectionString.TrimEnd(';') + ";Search Path=public";
            }

            IHostBuilder hostBuilder;
            if (string.Equals(service, "catalog", StringComparison.OrdinalIgnoreCase))
            {
                hostBuilder = CreateCatalogHost(writeConnectionString, tenantProvider);
            }
            else if (string.Equals(service, "customer", StringComparison.OrdinalIgnoreCase))
            {
                hostBuilder = CreateCustomerHost(writeConnectionString, tenantProvider);
            }
            else
            {
                // service is validated at program entry; this branch is unreachable
                logger.LogError("Unknown service '{Service}' during dedicated migration", service);
                failedCount++;
                continue;
            }

            IHost tenantHost = hostBuilder.Build();

            await ApplyEfCoreMigrationsAsync(tenantHost, service, logger);

            if (tenantProvider != DatabaseProvider.MySQL)
            {
                await tenantHost.SetupResources(ct);
            }

            logger.LogInformation(
                "Dedicated tenant {TenantIdentifier} migrated successfully", identifier);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Migration failed for dedicated tenant {TenantIdentifier} (service {Service}), continuing",
                identifier,
                service);
            failedCount++;
        }
    }

    return failedCount;
}

/// <summary>
/// Queries the Customer database for active tenants that have a dedicated database
/// for the specified service. PostgreSQL-specific (used only by *.postgres.yaml migration jobs).
/// </summary>
static async Task<IReadOnlyList<(string Identifier, string Provider)>> DiscoverDedicatedTenantsAsync(
    string customerConnectionString,
    string serviceName,
    ILogger logger,
    CancellationToken ct)
{
    logger.LogInformation(
        "Discovering dedicated tenants for service {Service} from Customer database",
        serviceName);

    await using var conn = new NpgsqlConnection(customerConnectionString);
    await conn.OpenAsync(ct);

    await using var cmd = conn.CreateCommand();
    cmd.CommandText = """
        SELECT DISTINCT t."Identifier",
               COALESCE(NULLIF(t."DatabaseProvider", ''), 'PostgreSQL') AS "DatabaseProvider"
        FROM   "Tenants" t
        INNER JOIN "TenantDatabaseMetadata" m ON m."TenantId" = t."Id"
        WHERE  t."DatabaseStrategy" = 'Dedicated'
          AND  m."ServiceName"       = @serviceName
          AND  t."IsActive"          = true
        """;

    cmd.Parameters.AddWithValue("serviceName", serviceName);

    await using var reader = await cmd.ExecuteReaderAsync(ct);
    var tenants = new List<(string Identifier, string Provider)>();

    while (await reader.ReadAsync(ct))
    {
        tenants.Add((reader.GetString(0), reader.GetString(1)));
    }

    logger.LogInformation(
        "Discovered {Count} dedicated tenant(s) for service {Service}",
        tenants.Count,
        serviceName);

    return tenants;
}

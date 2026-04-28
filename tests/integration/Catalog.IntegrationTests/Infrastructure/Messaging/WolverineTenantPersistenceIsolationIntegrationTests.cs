using Catalog.IntegrationTests.Shared;
using System.Diagnostics.CodeAnalysis;
using JasperFx;
using JasperFx.Resources;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using SharedKernel.Persistence.Database.MultiTenant;
using Shouldly;
using Testcontainers.PostgreSql;
using Wolverine;
using Wolverine.Postgresql;

namespace Catalog.IntegrationTests.Infrastructure.Messaging;

[Collection("SharedTestcontainers")]
public sealed class WolverineTenantPersistenceIsolationIntegrationTests
{
    private readonly SharedTestcontainersFixture sharedFixture;

    public WolverineTenantPersistenceIsolationIntegrationTests(SharedTestcontainersFixture sharedFixture)
    {
        this.sharedFixture = sharedFixture;
    }

    [Fact]
    public async Task ScheduledMessages_ShouldPersistToCorrectDatabase_ForSharedAndDedicatedTenants()
    {
        EnsurePostgresTestcontainersAvailable();

        await using PostgreSqlContainer sharedContainer = BuildTenantDatabaseContainer("tenant_shared");
        await using PostgreSqlContainer dedicatedContainer = new PostgreSqlBuilder("postgres:latest")
            .WithDatabase($"tenant_dedicated_{Guid.NewGuid():N}")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();
        await sharedContainer.StartAsync(TestContext.Current.CancellationToken);
        await dedicatedContainer.StartAsync(TestContext.Current.CancellationToken);

        string sharedConnectionString = sharedContainer.GetConnectionString();
        string dedicatedConnectionString = dedicatedContainer.GetConnectionString();

        string dedicatedTenantId = "tenant-dedicated-a";
        WolverineTenantConnectionSource tenantSource = new(sharedConnectionString);
        await tenantSource.AddTenantAsync(dedicatedTenantId, dedicatedConnectionString);

        using IHost host = Host
            .CreateDefaultBuilder()
            .UseWolverine(options =>
            {
                options.Discovery.IncludeAssembly(typeof(WolverineTenantPersistenceIsolationIntegrationTests).Assembly);
                options.UseSystemTextJsonForSerialization();
                options.Policies.UseDurableLocalQueues();

                options
                    .PersistMessagesWithPostgresql(sharedConnectionString, "wolverine")
                    .RegisterTenants(tenantSource)
                    .OverrideAutoCreateResources(AutoCreate.CreateOrUpdate);
            })
            .Build();

        await host.StartAsync(TestContext.Current.CancellationToken);
        await host.SetupResources(TestContext.Current.CancellationToken);

        IMessageBus messageBus = host.Services.GetRequiredService<IMessageBus>();

        string dedicatedMarker = $"ded-{Guid.NewGuid():N}";
        string sharedMarker = $"shr-{Guid.NewGuid():N}";

        DeliveryOptions dedicatedOptions = new()
        {
            TenantId = dedicatedTenantId,
        };
        dedicatedOptions.WithHeader("x-tenant-probe", dedicatedMarker);

        DeliveryOptions sharedOptions = new()
        {
            TenantId = "tenant-shared-b",
        };
        sharedOptions.WithHeader("x-tenant-probe", sharedMarker);

        await messageBus.ScheduleAsync(
            new TenantIsolationProbeMessage(dedicatedMarker),
            TimeSpan.FromMinutes(5),
            dedicatedOptions);

        await messageBus.ScheduleAsync(
            new TenantIsolationProbeMessage(sharedMarker),
            TimeSpan.FromMinutes(5),
            sharedOptions);

        await Task.Delay(TimeSpan.FromMilliseconds(500), TestContext.Current.CancellationToken);

        long dedicatedInDedicated = await CountMarkerOccurrencesAsync(dedicatedConnectionString, dedicatedMarker, TestContext.Current.CancellationToken);
        long dedicatedInShared = await CountMarkerOccurrencesAsync(sharedConnectionString, dedicatedMarker, TestContext.Current.CancellationToken);
        long sharedInShared = await CountMarkerOccurrencesAsync(sharedConnectionString, sharedMarker, TestContext.Current.CancellationToken);
        long sharedInDedicated = await CountMarkerOccurrencesAsync(dedicatedConnectionString, sharedMarker, TestContext.Current.CancellationToken);

        dedicatedInDedicated.ShouldBeGreaterThan(0);
        dedicatedInShared.ShouldBe(0);
        sharedInShared.ShouldBeGreaterThan(0);
        sharedInDedicated.ShouldBe(0);

        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ScheduledMessages_ShouldFallbackToShared_WhenDedicatedTenantIsDisabled()
    {
        EnsurePostgresTestcontainersAvailable();

        await using PostgreSqlContainer sharedContainer = BuildTenantDatabaseContainer("tenant_shared_disabled");
        await using PostgreSqlContainer dedicatedContainer = new PostgreSqlBuilder("postgres:latest")
            .WithDatabase($"tenant_dedicated_disabled_{Guid.NewGuid():N}")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();
        await sharedContainer.StartAsync(TestContext.Current.CancellationToken);
        await dedicatedContainer.StartAsync(TestContext.Current.CancellationToken);

        string sharedConnectionString = sharedContainer.GetConnectionString();
        string dedicatedConnectionString = dedicatedContainer.GetConnectionString();

        string dedicatedTenantId = "tenant-dedicated-disabled";
        WolverineTenantConnectionSource tenantSource = new(sharedConnectionString);
        await tenantSource.AddTenantAsync(dedicatedTenantId, dedicatedConnectionString);
        await tenantSource.DisableTenantAsync(dedicatedTenantId);

        using IHost host = Host
            .CreateDefaultBuilder()
            .UseWolverine(options =>
            {
                options.Discovery.IncludeAssembly(typeof(WolverineTenantPersistenceIsolationIntegrationTests).Assembly);
                options.UseSystemTextJsonForSerialization();
                options.Policies.UseDurableLocalQueues();

                options
                    .PersistMessagesWithPostgresql(sharedConnectionString, "wolverine")
                    .RegisterTenants(tenantSource)
                    .OverrideAutoCreateResources(AutoCreate.CreateOrUpdate);
            })
            .Build();

        await host.StartAsync(TestContext.Current.CancellationToken);
        await host.SetupResources(TestContext.Current.CancellationToken);

        IMessageBus messageBus = host.Services.GetRequiredService<IMessageBus>();

        string marker = $"disabled-{Guid.NewGuid():N}";
        DeliveryOptions options = new()
        {
            TenantId = dedicatedTenantId,
        };
        options.WithHeader("x-tenant-probe", marker);

        await messageBus.ScheduleAsync(
            new TenantIsolationProbeMessage(marker),
            TimeSpan.FromMinutes(5),
            options);

        await Task.Delay(TimeSpan.FromMilliseconds(500), TestContext.Current.CancellationToken);

        long markerInShared = await CountMarkerOccurrencesAsync(sharedConnectionString, marker, TestContext.Current.CancellationToken);
        long markerInDedicated = await CountMarkerOccurrencesAsync(dedicatedConnectionString, marker, TestContext.Current.CancellationToken);

        markerInShared.ShouldBeGreaterThan(0);
        markerInDedicated.ShouldBe(0);

        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ScheduledMessages_ShouldResolveDedicatedTenantOnDemand_WhenNotPreRegistered()
    {
        EnsurePostgresTestcontainersAvailable();

        await using PostgreSqlContainer sharedContainer = BuildTenantDatabaseContainer("tenant_shared_ondemand");
        await using PostgreSqlContainer dedicatedContainer = new PostgreSqlBuilder("postgres:latest")
            .WithDatabase($"tenant_dedicated_ondemand_{Guid.NewGuid():N}")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();
        await sharedContainer.StartAsync(TestContext.Current.CancellationToken);
        await dedicatedContainer.StartAsync(TestContext.Current.CancellationToken);

        string sharedConnectionString = sharedContainer.GetConnectionString();
        string dedicatedConnectionString = dedicatedContainer.GetConnectionString();

        string dedicatedTenantId = "tenant-dedicated-ondemand";
        WolverineTenantConnectionSource tenantSource = new(sharedConnectionString);

        int resolveCount = 0;
        tenantSource.SetMissingTenantResolver((tenantId, _) =>
        {
            Interlocked.Increment(ref resolveCount);
            return Task.FromResult<string?>(
                string.Equals(tenantId, dedicatedTenantId, StringComparison.Ordinal)
                    ? dedicatedConnectionString
                    : null);
        });

        using IHost host = Host
            .CreateDefaultBuilder()
            .UseWolverine(options =>
            {
                options.Discovery.IncludeAssembly(typeof(WolverineTenantPersistenceIsolationIntegrationTests).Assembly);
                options.UseSystemTextJsonForSerialization();
                options.Policies.UseDurableLocalQueues();

                options
                    .PersistMessagesWithPostgresql(sharedConnectionString, "wolverine")
                    .RegisterTenants(tenantSource)
                    .OverrideAutoCreateResources(AutoCreate.CreateOrUpdate);
            })
            .Build();

        await host.StartAsync(TestContext.Current.CancellationToken);
        await host.SetupResources(TestContext.Current.CancellationToken);

        IMessageBus messageBus = host.Services.GetRequiredService<IMessageBus>();

        string dedicatedMarker = $"ondemand-{Guid.NewGuid():N}";
        DeliveryOptions options = new()
        {
            TenantId = dedicatedTenantId,
        };
        options.WithHeader("x-tenant-probe", dedicatedMarker);

        await messageBus.ScheduleAsync(
            new TenantIsolationProbeMessage(dedicatedMarker),
            TimeSpan.FromMinutes(5),
            options);

        await Task.Delay(TimeSpan.FromMilliseconds(500), TestContext.Current.CancellationToken);

        long markerInDedicated = await CountMarkerOccurrencesAsync(dedicatedConnectionString, dedicatedMarker, TestContext.Current.CancellationToken);
        long markerInShared = await CountMarkerOccurrencesAsync(sharedConnectionString, dedicatedMarker, TestContext.Current.CancellationToken);

        markerInDedicated.ShouldBeGreaterThan(0);
        markerInShared.ShouldBe(0);
        resolveCount.ShouldBe(1);

        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ScheduledMessages_ShouldReflectTenantLifecycleMappings_ForPreUseTransitions()
    {
        EnsurePostgresTestcontainersAvailable();

        await using PostgreSqlContainer sharedContainer = BuildTenantDatabaseContainer("tenant_shared_lifecycle");
        await using PostgreSqlContainer dedicatedContainer = new PostgreSqlBuilder("postgres:latest")
            .WithDatabase($"tenant_dedicated_lifecycle_{Guid.NewGuid():N}")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();
        await sharedContainer.StartAsync(TestContext.Current.CancellationToken);
        await dedicatedContainer.StartAsync(TestContext.Current.CancellationToken);

        string sharedConnectionString = sharedContainer.GetConnectionString();
        string dedicatedConnectionString = dedicatedContainer.GetConnectionString();

        string strategyTenantId = "tenant-lifecycle-strategy";
        string disabledTenantId = "tenant-lifecycle-disabled";
        string reenabledTenantId = "tenant-lifecycle-reenabled";
        string removedTenantId = "tenant-lifecycle-removed";

        WolverineTenantConnectionSource tenantSource = new(sharedConnectionString);

        using IHost host = Host
            .CreateDefaultBuilder()
            .UseWolverine(options =>
            {
                options.Discovery.IncludeAssembly(typeof(WolverineTenantPersistenceIsolationIntegrationTests).Assembly);
                options.UseSystemTextJsonForSerialization();
                options.Policies.UseDurableLocalQueues();

                options
                    .PersistMessagesWithPostgresql(sharedConnectionString, "wolverine")
                    .RegisterTenants(tenantSource)
                    .OverrideAutoCreateResources(AutoCreate.CreateOrUpdate);
            })
            .Build();

        await host.StartAsync(TestContext.Current.CancellationToken);
        await host.SetupResources(TestContext.Current.CancellationToken);

        IMessageBus messageBus = host.Services.GetRequiredService<IMessageBus>();

        // Strategy change before first use should route to the latest mapping.
        await tenantSource.AddTenantAsync(strategyTenantId, sharedConnectionString);
        await tenantSource.AddTenantAsync(strategyTenantId, dedicatedConnectionString);
        string strategyMarker = $"lifecycle-strategy-{Guid.NewGuid():N}";
        await ScheduleProbeAsync(messageBus, strategyTenantId, strategyMarker);

        // Disabled tenant should fallback to shared.
        await tenantSource.AddTenantAsync(disabledTenantId, dedicatedConnectionString);
        await tenantSource.DisableTenantAsync(disabledTenantId);
        string disabledMarker = $"lifecycle-disabled-{Guid.NewGuid():N}";
        await ScheduleProbeAsync(messageBus, disabledTenantId, disabledMarker);

        // Re-enabled tenant (without prior resolution while disabled) should route dedicated.
        await tenantSource.AddTenantAsync(reenabledTenantId, dedicatedConnectionString);
        await tenantSource.DisableTenantAsync(reenabledTenantId);
        await tenantSource.EnableTenantAsync(reenabledTenantId);
        string reenabledMarker = $"lifecycle-reenabled-{Guid.NewGuid():N}";
        await ScheduleProbeAsync(messageBus, reenabledTenantId, reenabledMarker);

        // Removed tenant should fallback to shared.
        await tenantSource.AddTenantAsync(removedTenantId, dedicatedConnectionString);
        await tenantSource.RemoveTenantAsync(removedTenantId);
        string removedMarker = $"lifecycle-removed-{Guid.NewGuid():N}";
        await ScheduleProbeAsync(messageBus, removedTenantId, removedMarker);

        await Task.Delay(TimeSpan.FromMilliseconds(500), TestContext.Current.CancellationToken);

        long strategyInShared = await CountMarkerOccurrencesAsync(sharedConnectionString, strategyMarker, TestContext.Current.CancellationToken);
        long strategyInDedicated = await CountMarkerOccurrencesAsync(dedicatedConnectionString, strategyMarker, TestContext.Current.CancellationToken);
        long disabledInShared = await CountMarkerOccurrencesAsync(sharedConnectionString, disabledMarker, TestContext.Current.CancellationToken);
        long disabledInDedicated = await CountMarkerOccurrencesAsync(dedicatedConnectionString, disabledMarker, TestContext.Current.CancellationToken);
        long reenabledInShared = await CountMarkerOccurrencesAsync(sharedConnectionString, reenabledMarker, TestContext.Current.CancellationToken);
        long reenabledInDedicated = await CountMarkerOccurrencesAsync(dedicatedConnectionString, reenabledMarker, TestContext.Current.CancellationToken);
        long removedInShared = await CountMarkerOccurrencesAsync(sharedConnectionString, removedMarker, TestContext.Current.CancellationToken);
        long removedInDedicated = await CountMarkerOccurrencesAsync(dedicatedConnectionString, removedMarker, TestContext.Current.CancellationToken);

        strategyInDedicated.ShouldBeGreaterThan(0);
        strategyInShared.ShouldBe(0);

        disabledInShared.ShouldBeGreaterThan(0);
        disabledInDedicated.ShouldBe(0);

        reenabledInDedicated.ShouldBeGreaterThan(0);
        reenabledInShared.ShouldBe(0);

        removedInShared.ShouldBeGreaterThan(0);
        removedInDedicated.ShouldBe(0);

        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    private static async Task ScheduleProbeAsync(IMessageBus messageBus, string tenantId, string marker)
    {
        DeliveryOptions options = new()
        {
            TenantId = tenantId,
        };
        options.WithHeader("x-tenant-probe", marker);

        await messageBus.ScheduleAsync(
            new TenantIsolationProbeMessage(marker),
            TimeSpan.FromMinutes(5),
            options);
    }

    private void EnsurePostgresTestcontainersAvailable()
    {
        if (this.sharedFixture.UseSqliteFallback)
        {
            throw new InvalidOperationException(
                "Wolverine tenant persistence isolation test requires PostgreSQL testcontainers. " +
                "SQLite fallback is not allowed because it can produce false-positive passes.");
        }
    }

    private static PostgreSqlContainer BuildTenantDatabaseContainer(string namePrefix)
    {
        string databaseName = $"{namePrefix}_{Guid.NewGuid():N}";

        return new PostgreSqlBuilder("postgres:latest")
            .WithDatabase(databaseName)
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();
    }

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "Table and column names are read from PostgreSQL information_schema in test-only code, validated as safe identifiers, and escaped before interpolation.")]
    private static async Task<long> CountMarkerOccurrencesAsync(string connectionString, string marker, CancellationToken cancellationToken)
    {
        await using NpgsqlConnection conn = new(connectionString);
        await conn.OpenAsync(cancellationToken);

        const string columnsSql = """
            SELECT table_name, column_name, data_type
            FROM information_schema.columns
            WHERE table_schema = 'wolverine'
              AND data_type IN ('text', 'character varying', 'json', 'jsonb', 'bytea')
            """;

        var columns = new List<(string TableName, string ColumnName, string DataType)>();
        await using (NpgsqlCommand columnsCmd = new(columnsSql, conn))
        await using (NpgsqlDataReader reader = await columnsCmd.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                columns.Add((
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetString(2)));
            }
        }

        long totalMatches = 0;
        foreach ((string tableName, string columnName, string dataType) in columns)
        {
            if (!IsSafeIdentifier(tableName) || !IsSafeIdentifier(columnName))
            {
                continue;
            }

            string escapedTable = tableName.Replace("\"", "\"\"", StringComparison.Ordinal);
            string escapedColumn = columnName.Replace("\"", "\"\"", StringComparison.Ordinal);

            string expr = dataType == "bytea"
                ? $"encode(\"{escapedColumn}\", 'escape')"
                : $"CAST(\"{escapedColumn}\" AS text)";

            string sql = $"SELECT COUNT(*) FROM wolverine.\"{escapedTable}\" WHERE {expr} LIKE @pattern";

            await using NpgsqlCommand countCmd = new(sql, conn);
            countCmd.Parameters.AddWithValue("pattern", $"%{marker}%");

            object? value = await countCmd.ExecuteScalarAsync(cancellationToken);
            totalMatches += value is long l ? l : Convert.ToInt64(value);
        }

        return totalMatches;
    }

    private static bool IsSafeIdentifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        foreach (char c in value)
        {
            if (!(char.IsLetterOrDigit(c) || c == '_'))
            {
                return false;
            }
        }

        return true;
    }

    public sealed record TenantIsolationProbeMessage(string Marker);

    public static class TenantIsolationProbeHandler
    {
        public static Task Handle(TenantIsolationProbeMessage message)
        {
            _ = message;
            return Task.CompletedTask;
        }
    }
}

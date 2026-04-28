using JasperFx.Descriptors;
using SharedKernel.Persistence.Database.MultiTenant;
using Shouldly;

namespace SharedKernel.Persistence.UnitTests.Database.MultiTenant;

public sealed class WolverineTenantConnectionSourceTests
{
    [Fact]
    public async Task AddTenantAsync_ShouldStoreTenantConnection()
    {
        // Arrange
        WolverineTenantConnectionSource source = new("Host=shared-write;");

        // Act
        await source.AddTenantAsync("tenant-a", "Host=tenant-a-write;");
        string result = await source.FindAsync("tenant-a");

        // Assert
        result.ShouldBe("Host=tenant-a-write;");
        source.AllActive().ShouldContain("Host=tenant-a-write;");
        source.AllActiveByTenant().Count.ShouldBe(1);
        source.Cardinality.ShouldBe(DatabaseCardinality.DynamicMultiple);
    }

    [Fact]
    public async Task FindAsync_ShouldReturnDefaultConnection_WhenTenantNotRegistered()
    {
        // Arrange
        WolverineTenantConnectionSource source = new("Host=shared-write;");

        // Act
        string result = await source.FindAsync("missing-tenant");

        // Assert
        result.ShouldBe("Host=shared-write;");
    }

    [Fact]
    public async Task DisableAndEnableTenant_ShouldMoveTenantBetweenActiveAndDisabled()
    {
        // Arrange
        WolverineTenantConnectionSource source = new("Host=shared-write;");
        await source.AddTenantAsync("tenant-a", "Host=tenant-a-write;");

        // Act
        await source.DisableTenantAsync("tenant-a");
        IReadOnlyList<string> disabled = await source.AllDisabledAsync();
        string afterDisable = await source.FindAsync("tenant-a");

        await source.EnableTenantAsync("tenant-a");
        string afterEnable = await source.FindAsync("tenant-a");

        // Assert
        disabled.ShouldContain("tenant-a");
        afterDisable.ShouldBe("Host=shared-write;");
        afterEnable.ShouldBe("Host=tenant-a-write;");
    }

    [Fact]
    public async Task RemoveTenantAsync_ShouldRemoveFromActiveAndDisabled()
    {
        // Arrange
        WolverineTenantConnectionSource source = new("Host=shared-write;");
        await source.AddTenantAsync("tenant-a", "Host=tenant-a-write;");
        await source.DisableTenantAsync("tenant-a");

        // Act
        await source.RemoveTenantAsync("tenant-a");
        IReadOnlyList<string> disabled = await source.AllDisabledAsync();
        string result = await source.FindAsync("tenant-a");

        // Assert
        disabled.ShouldNotContain("tenant-a");
        source.AllActiveByTenant().Count.ShouldBe(0);
        result.ShouldBe("Host=shared-write;");
    }

    [Fact]
    public async Task FindAsync_ShouldKeepConnectionsIsolatedAcrossTenants()
    {
        // Arrange
        WolverineTenantConnectionSource source = new("Host=shared-write;");
        await source.AddTenantAsync("tenant-a", "Host=tenant-a-write;");
        await source.AddTenantAsync("tenant-b", "Host=tenant-b-write;");

        // Act
        string tenantAConnection = await source.FindAsync("tenant-a");
        string tenantBConnection = await source.FindAsync("tenant-b");
        string unknownTenantConnection = await source.FindAsync("tenant-c");

        // Assert
        tenantAConnection.ShouldBe("Host=tenant-a-write;");
        tenantBConnection.ShouldBe("Host=tenant-b-write;");
        unknownTenantConnection.ShouldBe("Host=shared-write;");
    }

    [Fact]
    public async Task FindAsync_ShouldResolveMissingTenantOnDemand_AndCacheResult()
    {
        // Arrange
        WolverineTenantConnectionSource source = new("Host=shared-write;");
        int resolveCount = 0;

        source.SetMissingTenantResolver((tenantId, _) =>
        {
            Interlocked.Increment(ref resolveCount);
            return Task.FromResult<string?>(tenantId == "tenant-dynamic" ? "Host=tenant-dynamic-write;" : null);
        });

        // Act
        string firstResult = await source.FindAsync("tenant-dynamic");
        string secondResult = await source.FindAsync("tenant-dynamic");

        // Assert
        firstResult.ShouldBe("Host=tenant-dynamic-write;");
        secondResult.ShouldBe("Host=tenant-dynamic-write;");
        resolveCount.ShouldBe(1);
    }

    [Fact]
    public async Task FindAsync_ShouldUseSingleFlight_ForConcurrentMissingLookups()
    {
        // Arrange
        WolverineTenantConnectionSource source = new("Host=shared-write;");
        int resolveCount = 0;

        source.SetMissingTenantResolver(async (_, _) =>
        {
            Interlocked.Increment(ref resolveCount);
            await Task.Delay(50, TestContext.Current.CancellationToken);
            return "Host=tenant-concurrent-write;";
        });

        // Act
        Task<string> firstLookup = source.FindAsync("tenant-concurrent").AsTask();
        Task<string> secondLookup = source.FindAsync("tenant-concurrent").AsTask();

        string[] results = await Task.WhenAll(firstLookup, secondLookup);

        // Assert
        results[0].ShouldBe("Host=tenant-concurrent-write;");
        results[1].ShouldBe("Host=tenant-concurrent-write;");
        resolveCount.ShouldBe(1);
    }

    [Fact]
    public async Task FindAsync_ShouldNotResolve_WhenTenantIsDisabled()
    {
        // Arrange
        WolverineTenantConnectionSource source = new("Host=shared-write;");
        int resolveCount = 0;

        source.SetMissingTenantResolver((_, _) =>
        {
            Interlocked.Increment(ref resolveCount);
            return Task.FromResult<string?>("Host=unexpected;");
        });

        await source.AddTenantAsync("tenant-disabled", "Host=tenant-disabled-write;");
        await source.DisableTenantAsync("tenant-disabled");

        // Act
        string result = await source.FindAsync("tenant-disabled");

        // Assert
        result.ShouldBe("Host=shared-write;");
        resolveCount.ShouldBe(0);
    }

    [Fact]
    public async Task FindAsync_ShouldReturnShared_WhenResolverReturnsNull()
    {
        // Arrange
        WolverineTenantConnectionSource source = new("Host=shared-write;");

        source.SetMissingTenantResolver((_, _) => Task.FromResult<string?>(null));

        // Act
        string result = await source.FindAsync("tenant-unknown");

        // Assert
        result.ShouldBe("Host=shared-write;");
    }

    [Fact]
    public async Task AddTenantAsync_ShouldOverwriteExistingTenantMapping()
    {
        // Arrange
        WolverineTenantConnectionSource source = new("Host=shared-write;");

        // Act
        await source.AddTenantAsync("tenant-a", "Host=tenant-a-write-v1;");
        await source.AddTenantAsync("tenant-a", "Host=tenant-a-write-v2;");
        string result = await source.FindAsync("tenant-a");

        // Assert
        result.ShouldBe("Host=tenant-a-write-v2;");
    }

    [Fact]
    public async Task FindAsync_ShouldResolveAgainAfterTenantRemoval_ForSecretRotation()
    {
        // Arrange
        WolverineTenantConnectionSource source = new("Host=shared-write;");
        int resolveCount = 0;
        string currentConnection = "Host=tenant-rotated-write-v1;";

        source.SetMissingTenantResolver((_, _) =>
        {
            Interlocked.Increment(ref resolveCount);
            return Task.FromResult<string?>(currentConnection);
        });

        // Act
        string firstResult = await source.FindAsync("tenant-rotation");

        currentConnection = "Host=tenant-rotated-write-v2;";
        await source.RemoveTenantAsync("tenant-rotation");

        string secondResult = await source.FindAsync("tenant-rotation");

        // Assert
        firstResult.ShouldBe("Host=tenant-rotated-write-v1;");
        secondResult.ShouldBe("Host=tenant-rotated-write-v2;");
        resolveCount.ShouldBe(2);
    }

    [Fact]
    public async Task FindAsync_ShouldHandleHighCardinalityMisses_WithSharedFallback()
    {
        // Arrange
        WolverineTenantConnectionSource source = new("Host=shared-write;");
        source.SetMissingTenantResolver((_, _) => Task.FromResult<string?>(null));

        // Act
        Task<string>[] lookups = Enumerable
            .Range(0, 250)
            .Select(index => source.FindAsync($"tenant-miss-{index}").AsTask())
            .ToArray();

        string[] results = await Task.WhenAll(lookups);

        // Assert
        results.ShouldAllBe(connection => connection == "Host=shared-write;");
    }

    [Fact]
    public async Task FindAsync_ShouldThrowInStrictMode_WhenTenantMissingAndNoResolverConfigured()
    {
        // Arrange
        WolverineTenantConnectionSource source = new("Host=shared-write;");
        source.SetStrictTenantResolution(true);

        // Act & Assert
        await Should.ThrowAsync<TenantConnectionNotFoundException>(async () =>
        {
            _ = await source.FindAsync("tenant-strict-missing");
        });
    }

    [Fact]
    public async Task FindAsync_ShouldThrowInStrictMode_WhenResolverReturnsNull()
    {
        // Arrange
        WolverineTenantConnectionSource source = new("Host=shared-write;");
        source.SetStrictTenantResolution(true);
        source.SetMissingTenantResolver((_, _) => Task.FromResult<string?>(null));

        // Act & Assert
        await Should.ThrowAsync<TenantConnectionNotFoundException>(async () =>
        {
            _ = await source.FindAsync("tenant-strict-null");
        });
    }
}

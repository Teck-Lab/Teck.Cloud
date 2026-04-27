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
}

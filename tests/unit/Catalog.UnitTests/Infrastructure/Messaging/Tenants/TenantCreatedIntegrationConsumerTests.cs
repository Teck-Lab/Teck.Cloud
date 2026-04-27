using Catalog.Infrastructure.Messaging.Tenants;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SharedKernel.Events;
using SharedKernel.Persistence.Database.MultiTenant;
using Shouldly;

namespace Catalog.UnitTests.Infrastructure.Messaging.Tenants;

public sealed class TenantCreatedIntegrationConsumerTests
{
    private readonly IVaultTenantConnectionProvider vaultTenantConnectionProvider;
    private readonly WolverineTenantConnectionSource tenantConnectionSource;
    private readonly TenantCreatedIntegrationConsumer sut;

    public TenantCreatedIntegrationConsumerTests()
    {
        this.vaultTenantConnectionProvider = Substitute.For<IVaultTenantConnectionProvider>();
        this.tenantConnectionSource = new WolverineTenantConnectionSource("Host=catalog-shared-write;");

        ILogger<TenantCreatedIntegrationConsumer> logger = Substitute.For<ILogger<TenantCreatedIntegrationConsumer>>();
        this.sut = new TenantCreatedIntegrationConsumer(
            this.tenantConnectionSource,
            this.vaultTenantConnectionProvider,
            logger);
    }

    [Fact]
    public async Task Handle_ShouldUseSharedConnection_WhenDatabaseStrategyIsShared()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        TenantCreatedIntegrationEvent integrationEvent = new(
            tenantId,
            "tenant-slug",
            "Tenant",
            "Shared",
            "PostgreSQL");

        // Act
        await this.sut.Handle(integrationEvent, TestContext.Current.CancellationToken);
        string result = await this.tenantConnectionSource.FindAsync(tenantId.ToString("D"));

        // Assert
        result.ShouldBe("Host=catalog-shared-write;");
        _ = this.vaultTenantConnectionProvider.DidNotReceiveWithAnyArgs().GetAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Handle_ShouldUseVaultConnection_WhenDatabaseStrategyIsDedicated()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        TenantCreatedIntegrationEvent integrationEvent = new(
            tenantId,
            "tenant-slug",
            "Tenant",
            "Dedicated",
            "PostgreSQL");

        this.vaultTenantConnectionProvider
            .GetAsync("tenant-slug", Arg.Any<CancellationToken>())
            .Returns(("Host=catalog-dedicated-write;", null));

        // Act
        await this.sut.Handle(integrationEvent, TestContext.Current.CancellationToken);
        string result = await this.tenantConnectionSource.FindAsync(tenantId.ToString("D"));

        // Assert
        result.ShouldBe("Host=catalog-dedicated-write;");
        _ = this.vaultTenantConnectionProvider
            .Received(1)
            .GetAsync("tenant-slug", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldFallbackToTenantId_WhenIdentifierIsEmpty()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        string tenantIdString = tenantId.ToString("D");
        TenantCreatedIntegrationEvent integrationEvent = new(
            tenantId,
            string.Empty,
            "Tenant",
            "External",
            "PostgreSQL");

        this.vaultTenantConnectionProvider
            .GetAsync(tenantIdString, Arg.Any<CancellationToken>())
            .Returns(("Host=catalog-fallback-write;", null));

        // Act
        await this.sut.Handle(integrationEvent, TestContext.Current.CancellationToken);
        string result = await this.tenantConnectionSource.FindAsync(tenantIdString);

        // Assert
        result.ShouldBe("Host=catalog-fallback-write;");
        _ = this.vaultTenantConnectionProvider
            .Received(1)
            .GetAsync(tenantIdString, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldKeepSharedAndDedicatedTenantsIsolated()
    {
        // Arrange
        Guid dedicatedTenantId = Guid.NewGuid();
        Guid sharedTenantId = Guid.NewGuid();

        TenantCreatedIntegrationEvent dedicatedEvent = new(
            dedicatedTenantId,
            "tenant-dedicated",
            "Dedicated Tenant",
            "Dedicated",
            "PostgreSQL");

        TenantCreatedIntegrationEvent sharedEvent = new(
            sharedTenantId,
            "tenant-shared",
            "Shared Tenant",
            "Shared",
            "PostgreSQL");

        this.vaultTenantConnectionProvider
            .GetAsync("tenant-dedicated", Arg.Any<CancellationToken>())
            .Returns(("Host=catalog-dedicated-write;", null));

        // Act
        await this.sut.Handle(dedicatedEvent, TestContext.Current.CancellationToken);
        await this.sut.Handle(sharedEvent, TestContext.Current.CancellationToken);

        string dedicatedResult = await this.tenantConnectionSource.FindAsync(dedicatedTenantId.ToString("D"));
        string sharedResult = await this.tenantConnectionSource.FindAsync(sharedTenantId.ToString("D"));

        // Assert
        dedicatedResult.ShouldBe("Host=catalog-dedicated-write;");
        sharedResult.ShouldBe("Host=catalog-shared-write;");

        _ = this.vaultTenantConnectionProvider
            .Received(1)
            .GetAsync("tenant-dedicated", Arg.Any<CancellationToken>());
        _ = this.vaultTenantConnectionProvider
            .DidNotReceive()
            .GetAsync("tenant-shared", Arg.Any<CancellationToken>());
    }
}

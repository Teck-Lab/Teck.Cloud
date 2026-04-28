using Customer.Api.Infrastructure.Messaging.Tenants;
using Customer.Application.Tenants.ReadModels;
using Customer.Application.Tenants.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SharedKernel.Persistence.Database.MultiTenant;
using Shouldly;

namespace Customer.UnitTests.Infrastructure.Messaging.Tenants;

public sealed class CustomerTenantConnectionMissResolverTests
{
    private readonly ITenantReadRepository tenantReadRepository;
    private readonly IVaultTenantConnectionProvider vaultTenantConnectionProvider;
    private readonly WolverineTenantConnectionSource tenantConnectionSource;
    private readonly CustomerTenantConnectionMissResolver sut;

    public CustomerTenantConnectionMissResolverTests()
    {
        this.tenantReadRepository = Substitute.For<ITenantReadRepository>();
        this.vaultTenantConnectionProvider = Substitute.For<IVaultTenantConnectionProvider>();
        this.tenantConnectionSource = new WolverineTenantConnectionSource("Host=customer-shared-write;");

        this.sut = new CustomerTenantConnectionMissResolver(
            NullLogger<CustomerTenantConnectionMissResolver>.Instance,
            this.tenantReadRepository,
            this.tenantConnectionSource,
            this.vaultTenantConnectionProvider);
    }

    [Fact]
    public async Task ResolveAsync_ShouldReturnNull_WhenTenantIdIsInvalid()
    {
        // Act
        string? result = await this.sut.ResolveAsync("not-a-guid", TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeNull();
        _ = this.tenantReadRepository.DidNotReceiveWithAnyArgs()
            .GetDatabaseInfoByIdAsync(default, default, TestContext.Current.CancellationToken);
        _ = this.vaultTenantConnectionProvider.DidNotReceiveWithAnyArgs()
            .GetAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ResolveAsync_ShouldReturnNull_WhenTenantMetadataMissing()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        this.tenantReadRepository
            .GetDatabaseInfoByIdAsync(tenantId, "customer", Arg.Any<CancellationToken>())
            .Returns((TenantDatabaseInfoReadModel?)null);

        // Act
        string? result = await this.sut.ResolveAsync(tenantId.ToString("D"), TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task ResolveAsync_ShouldReturnSharedConnection_WhenStrategyIsShared()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        this.tenantReadRepository
            .GetDatabaseInfoByIdAsync(tenantId, "customer", Arg.Any<CancellationToken>())
            .Returns(new TenantDatabaseInfoReadModel
            {
                TenantId = tenantId,
                Identifier = "tenant-shared",
                DatabaseStrategy = "Shared",
                DatabaseProvider = "PostgreSQL",
            });

        // Act
        string? result = await this.sut.ResolveAsync(tenantId.ToString("D"), TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBe("Host=customer-shared-write;");
        _ = this.vaultTenantConnectionProvider.DidNotReceiveWithAnyArgs().GetAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ResolveAsync_ShouldReturnDedicatedConnection_FromIdentifier()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        this.tenantReadRepository
            .GetDatabaseInfoByIdAsync(tenantId, "customer", Arg.Any<CancellationToken>())
            .Returns(new TenantDatabaseInfoReadModel
            {
                TenantId = tenantId,
                Identifier = "tenant-dedicated",
                DatabaseStrategy = "Dedicated",
                DatabaseProvider = "PostgreSQL",
            });

        this.vaultTenantConnectionProvider
            .GetAsync("tenant-dedicated", Arg.Any<CancellationToken>())
            .Returns(("Host=customer-dedicated-write;", null));

        // Act
        string? result = await this.sut.ResolveAsync(tenantId.ToString("D"), TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBe("Host=customer-dedicated-write;");
        _ = this.vaultTenantConnectionProvider
            .Received(1)
            .GetAsync("tenant-dedicated", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResolveAsync_ShouldFallbackToTenantId_WhenIdentifierLookupMissing()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        string tenantIdText = tenantId.ToString("D");

        this.tenantReadRepository
            .GetDatabaseInfoByIdAsync(tenantId, "customer", Arg.Any<CancellationToken>())
            .Returns(new TenantDatabaseInfoReadModel
            {
                TenantId = tenantId,
                Identifier = "tenant-dedicated",
                DatabaseStrategy = "External",
                DatabaseProvider = "PostgreSQL",
            });

        this.vaultTenantConnectionProvider
            .GetAsync("tenant-dedicated", Arg.Any<CancellationToken>())
            .Returns<Task<(string Write, string? Read)>>(_ => throw new TenantConnectionNotFoundException("missing"));

        this.vaultTenantConnectionProvider
            .GetAsync(tenantIdText, Arg.Any<CancellationToken>())
            .Returns(("Host=customer-fallback-write;", null));

        // Act
        string? result = await this.sut.ResolveAsync(tenantIdText, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBe("Host=customer-fallback-write;");
        _ = this.vaultTenantConnectionProvider.Received(1)
            .GetAsync("tenant-dedicated", Arg.Any<CancellationToken>());
        _ = this.vaultTenantConnectionProvider.Received(1)
            .GetAsync(tenantIdText, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResolveAsync_ShouldReturnNull_WhenVaultLookupFailsUnexpectedly()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        this.tenantReadRepository
            .GetDatabaseInfoByIdAsync(tenantId, "customer", Arg.Any<CancellationToken>())
            .Returns(new TenantDatabaseInfoReadModel
            {
                TenantId = tenantId,
                Identifier = "tenant-dedicated",
                DatabaseStrategy = "Dedicated",
                DatabaseProvider = "PostgreSQL",
            });

        this.vaultTenantConnectionProvider
            .GetAsync("tenant-dedicated", Arg.Any<CancellationToken>())
            .Returns<Task<(string Write, string? Read)>>(_ => throw new InvalidOperationException("vault unavailable"));

        // Act
        string? result = await this.sut.ResolveAsync(tenantId.ToString("D"), TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task ResolveAsync_ShouldTreatUnknownStrategyAsShared()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        this.tenantReadRepository
            .GetDatabaseInfoByIdAsync(tenantId, "customer", Arg.Any<CancellationToken>())
            .Returns(new TenantDatabaseInfoReadModel
            {
                TenantId = tenantId,
                Identifier = "tenant-unknown",
                DatabaseStrategy = "UnexpectedStrategy",
                DatabaseProvider = "PostgreSQL",
            });

        // Act
        string? result = await this.sut.ResolveAsync(tenantId.ToString("D"), TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBe("Host=customer-shared-write;");
        _ = this.vaultTenantConnectionProvider.DidNotReceiveWithAnyArgs().GetAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ResolveAsync_ShouldUseTenantId_WhenIdentifierIsEmptyForDedicatedTenant()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        string tenantIdText = tenantId.ToString("D");

        this.tenantReadRepository
            .GetDatabaseInfoByIdAsync(tenantId, "customer", Arg.Any<CancellationToken>())
            .Returns(new TenantDatabaseInfoReadModel
            {
                TenantId = tenantId,
                Identifier = string.Empty,
                DatabaseStrategy = "Dedicated",
                DatabaseProvider = "PostgreSQL",
            });

        this.vaultTenantConnectionProvider
            .GetAsync(tenantIdText, Arg.Any<CancellationToken>())
            .Returns(("Host=customer-dedicated-write-by-id;", null));

        // Act
        string? result = await this.sut.ResolveAsync(tenantIdText, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBe("Host=customer-dedicated-write-by-id;");
        _ = this.vaultTenantConnectionProvider.Received(1)
            .GetAsync(tenantIdText, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResolveAsync_ShouldReturnNull_WhenIdentifierAndTenantIdLookupsAreMissing()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        string tenantIdText = tenantId.ToString("D");

        this.tenantReadRepository
            .GetDatabaseInfoByIdAsync(tenantId, "customer", Arg.Any<CancellationToken>())
            .Returns(new TenantDatabaseInfoReadModel
            {
                TenantId = tenantId,
                Identifier = "tenant-dedicated",
                DatabaseStrategy = "Dedicated",
                DatabaseProvider = "PostgreSQL",
            });

        this.vaultTenantConnectionProvider
            .GetAsync("tenant-dedicated", Arg.Any<CancellationToken>())
            .Returns<Task<(string Write, string? Read)>>(_ => throw new TenantConnectionNotFoundException("missing by identifier"));

        this.vaultTenantConnectionProvider
            .GetAsync(tenantIdText, Arg.Any<CancellationToken>())
            .Returns<Task<(string Write, string? Read)>>(_ => throw new TenantConnectionNotFoundException("missing by tenant id"));

        // Act
        string? result = await this.sut.ResolveAsync(tenantIdText, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeNull();
        _ = this.vaultTenantConnectionProvider.Received(1)
            .GetAsync("tenant-dedicated", Arg.Any<CancellationToken>());
        _ = this.vaultTenantConnectionProvider.Received(1)
            .GetAsync(tenantIdText, Arg.Any<CancellationToken>());
    }
}

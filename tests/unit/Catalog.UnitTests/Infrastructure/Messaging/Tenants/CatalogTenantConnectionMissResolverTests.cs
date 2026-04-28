using Catalog.Api.Infrastructure.Messaging.Tenants;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SharedKernel.Grpc.Contracts.Remote.V1.Tenants;
using SharedKernel.Persistence.Database.MultiTenant;
using Shouldly;

namespace Catalog.UnitTests.Infrastructure.Messaging.Tenants;

public sealed class CatalogTenantConnectionMissResolverTests
{
    private readonly TestCatalogTenantDatabaseInfoClient tenantDatabaseInfoClient;
    private readonly IVaultTenantConnectionProvider vaultTenantConnectionProvider;
    private readonly WolverineTenantConnectionSource tenantConnectionSource;
    private readonly CatalogTenantConnectionMissResolver sut;

    public CatalogTenantConnectionMissResolverTests()
    {
        this.tenantDatabaseInfoClient = new TestCatalogTenantDatabaseInfoClient();
        this.vaultTenantConnectionProvider = Substitute.For<IVaultTenantConnectionProvider>();
        this.tenantConnectionSource = new WolverineTenantConnectionSource("Host=catalog-shared-write;");

        this.sut = new CatalogTenantConnectionMissResolver(
            NullLogger<CatalogTenantConnectionMissResolver>.Instance,
            this.tenantDatabaseInfoClient,
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
        this.tenantDatabaseInfoClient.CallCount.ShouldBe(0);
    }

    [Fact]
    public async Task ResolveAsync_ShouldReturnNull_WhenTenantInfoIsNull()
    {
        // Arrange
        string tenantId = Guid.NewGuid().ToString("D");
        this.tenantDatabaseInfoClient.SetHandler((_, _) => Task.FromResult<TenantDatabaseInfoRpcResult?>(null));

        // Act
        string? result = await this.sut.ResolveAsync(tenantId, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task ResolveAsync_ShouldReturnNull_WhenTenantNotFound()
    {
        // Arrange
        string tenantId = Guid.NewGuid().ToString("D");
        this.tenantDatabaseInfoClient.SetHandler((_, _) => Task.FromResult<TenantDatabaseInfoRpcResult?>(new TenantDatabaseInfoRpcResult
            {
                Found = false,
                ErrorDetail = "Tenant not found",
            }));

        // Act
        string? result = await this.sut.ResolveAsync(tenantId, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task ResolveAsync_ShouldReturnSharedConnection_WhenStrategyIsShared()
    {
        // Arrange
        string tenantId = Guid.NewGuid().ToString("D");
        this.tenantDatabaseInfoClient.SetHandler((_, _) => Task.FromResult<TenantDatabaseInfoRpcResult?>(new TenantDatabaseInfoRpcResult
            {
                Found = true,
                TenantId = tenantId,
                Identifier = "tenant-shared",
                DatabaseStrategy = "Shared",
                DatabaseProvider = "PostgreSQL",
            }));

        // Act
        string? result = await this.sut.ResolveAsync(tenantId, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBe("Host=catalog-shared-write;");
        _ = this.vaultTenantConnectionProvider.DidNotReceiveWithAnyArgs()
            .GetAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ResolveAsync_ShouldUseIdentifierForDedicatedLookup()
    {
        // Arrange
        string tenantId = Guid.NewGuid().ToString("D");
        this.tenantDatabaseInfoClient.SetHandler((_, _) => Task.FromResult<TenantDatabaseInfoRpcResult?>(new TenantDatabaseInfoRpcResult
            {
                Found = true,
                TenantId = tenantId,
                Identifier = "tenant-dedicated",
                DatabaseStrategy = "Dedicated",
                DatabaseProvider = "PostgreSQL",
            }));

        this.vaultTenantConnectionProvider
            .GetAsync("tenant-dedicated", Arg.Any<CancellationToken>())
            .Returns(("Host=catalog-dedicated-write;", null));

        // Act
        string? result = await this.sut.ResolveAsync(tenantId, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBe("Host=catalog-dedicated-write;");
        _ = this.vaultTenantConnectionProvider.Received(1)
            .GetAsync("tenant-dedicated", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResolveAsync_ShouldFallbackToTenantId_WhenIdentifierLookupMissing()
    {
        // Arrange
        string tenantId = Guid.NewGuid().ToString("D");
        this.tenantDatabaseInfoClient.SetHandler((_, _) => Task.FromResult<TenantDatabaseInfoRpcResult?>(new TenantDatabaseInfoRpcResult
            {
                Found = true,
                TenantId = tenantId,
                Identifier = "tenant-dedicated",
                DatabaseStrategy = "External",
                DatabaseProvider = "PostgreSQL",
            }));

        this.vaultTenantConnectionProvider
            .GetAsync("tenant-dedicated", Arg.Any<CancellationToken>())
            .Returns<Task<(string Write, string? Read)>>(_ => throw new TenantConnectionNotFoundException("missing by identifier"));

        this.vaultTenantConnectionProvider
            .GetAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(("Host=catalog-fallback-write;", null));

        // Act
        string? result = await this.sut.ResolveAsync(tenantId, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBe("Host=catalog-fallback-write;");
        _ = this.vaultTenantConnectionProvider.Received(1)
            .GetAsync("tenant-dedicated", Arg.Any<CancellationToken>());
        _ = this.vaultTenantConnectionProvider.Received(1)
            .GetAsync(tenantId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResolveAsync_ShouldReturnNull_WhenIdentifierAndTenantIdLookupsMissing()
    {
        // Arrange
        string tenantId = Guid.NewGuid().ToString("D");
        this.tenantDatabaseInfoClient.SetHandler((_, _) => Task.FromResult<TenantDatabaseInfoRpcResult?>(new TenantDatabaseInfoRpcResult
            {
                Found = true,
                TenantId = tenantId,
                Identifier = "tenant-dedicated",
                DatabaseStrategy = "Dedicated",
                DatabaseProvider = "PostgreSQL",
            }));

        this.vaultTenantConnectionProvider
            .GetAsync("tenant-dedicated", Arg.Any<CancellationToken>())
            .Returns<Task<(string Write, string? Read)>>(_ => throw new TenantConnectionNotFoundException("missing by identifier"));

        this.vaultTenantConnectionProvider
            .GetAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns<Task<(string Write, string? Read)>>(_ => throw new TenantConnectionNotFoundException("missing by tenant id"));

        // Act
        string? result = await this.sut.ResolveAsync(tenantId, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task ResolveAsync_ShouldReturnNull_WhenLookupClientThrows()
    {
        // Arrange
        string tenantId = Guid.NewGuid().ToString("D");
        this.tenantDatabaseInfoClient.SetHandler((_, _) => throw new InvalidOperationException("rpc down"));

        // Act
        string? result = await this.sut.ResolveAsync(tenantId, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeNull();
    }

    private sealed class TestCatalogTenantDatabaseInfoClient : ICatalogTenantDatabaseInfoClient
    {
        private Func<string, CancellationToken, Task<TenantDatabaseInfoRpcResult?>> handler =
            (_, _) => Task.FromResult<TenantDatabaseInfoRpcResult?>(null);

        public int CallCount { get; private set; }

        public void SetHandler(Func<string, CancellationToken, Task<TenantDatabaseInfoRpcResult?>> handler)
        {
            this.handler = handler;
        }

        public Task<TenantDatabaseInfoRpcResult?> GetTenantDatabaseInfoAsync(string tenantId, CancellationToken cancellationToken)
        {
            this.CallCount++;
            return this.handler(tenantId, cancellationToken);
        }
    }
}

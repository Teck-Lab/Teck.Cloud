using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using NSubstitute;
using SharedKernel.Core.Database;
using SharedKernel.Infrastructure.MultiTenant;
using SharedKernel.Persistence.Caching;
using Shouldly;
using ZiggyCreatures.Caching.Fusion;

namespace SharedKernel.Persistence.UnitTests.Caching;

public sealed class GenericCacheServiceTests
{
    [Fact]
    public void GenerateCacheKey_ShouldIncludeTenantSegment_WhenTenantIdIsPresent()
    {
        // Arrange
        var fusionCache = Substitute.For<IFusionCache>();
        var repository = Substitute.For<IGenericReadRepository<DummyEntity, Guid>>();
        var tenantAccessor = Substitute.For<IMultiTenantContextAccessor<TenantDetails>>();
        tenantAccessor.MultiTenantContext.Returns(new MultiTenantContext<TenantDetails>(new TenantDetails { Id = "tenant-a" }));

        var service = new GenericCacheService<DummyEntity, Guid>(fusionCache, repository, tenantAccessor);

        // Act
        string key = service.GenerateCacheKey("123");

        // Assert
        key.ShouldBe("DummyEntity:tenant:tenant-a:123");
    }

    [Fact]
    public void GenerateCacheKey_ShouldNotIncludeTenantSegment_WhenTenantAccessorIsNull()
    {
        // Arrange
        var fusionCache = Substitute.For<IFusionCache>();
        var repository = Substitute.For<IGenericReadRepository<DummyEntity, Guid>>();

        var service = new GenericCacheService<DummyEntity, Guid>(fusionCache, repository, tenantContextAccessor: null);

        // Act
        string key = service.GenerateCacheKey("123");

        // Assert
        key.ShouldBe("DummyEntity:123");
    }

    [Fact]
    public void GenerateCacheKey_ShouldNotIncludeTenantSegment_WhenTenantIdIsMissing()
    {
        // Arrange
        var fusionCache = Substitute.For<IFusionCache>();
        var repository = Substitute.For<IGenericReadRepository<DummyEntity, Guid>>();
        var tenantAccessor = Substitute.For<IMultiTenantContextAccessor<TenantDetails>>();
        tenantAccessor.MultiTenantContext.Returns(new MultiTenantContext<TenantDetails>(new TenantDetails { Id = string.Empty }));

        var service = new GenericCacheService<DummyEntity, Guid>(fusionCache, repository, tenantAccessor);

        // Act
        string key = service.GenerateCacheKey("123");

        // Assert
        key.ShouldBe("DummyEntity:123");
    }

    internal sealed class DummyEntity
    {
    }
}

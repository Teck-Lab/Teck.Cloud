#nullable enable
using Catalog.Application.Brands.ReadModels;
using Catalog.Application.Brands.Repositories;
using Catalog.Domain.Entities.BrandAggregate;
using Catalog.Domain.Entities.BrandAggregate.Repositories;
using Catalog.Infrastructure.Caching;
using Catalog.Infrastructure.Persistence;
using Catalog.Infrastructure.Persistence.Repositories.Read;
using Catalog.Infrastructure.Persistence.Repositories.Write;
using Catalog.IntegrationTests.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Core.Database;
using SharedKernel.Persistence.Database.EFCore;
using Shouldly;

namespace Catalog.IntegrationTests.Infrastructure.Brands
{
    [Collection("SharedTestcontainers")]
    public class BrandCacheIntegrationTests : BaseReadCacheTestFixture<ApplicationReadDbContext>
    {
        private BrandCache _cache = null!;
        private IBrandReadRepository _readRepository = null!;
        private ApplicationWriteDbContext _writeDbContext = null!;
        private IBrandWriteRepository _writeRepository = null!;
        private IUnitOfWork _unitOfWork = null!;

        public BrandCacheIntegrationTests(SharedTestcontainersFixture sharedFixture)
            : base(sharedFixture) { }

        protected override ApplicationReadDbContext CreateReadDbContext(DbContextOptions<ApplicationReadDbContext> options)
        {
            // Provide null for ITenantInfo and DatabaseStrategy.Single as defaults for testing
            return new ApplicationReadDbContext(options);
        }

        public override async ValueTask InitializeAsync()
        {
            await base.InitializeAsync();
            var httpContextAccessor = new HttpContextAccessor();
            _readRepository = new BrandReadRepository(ReadDbContext);

            // Set up write context and repository for test data setup
            var writeOptions = new DbContextOptionsBuilder<ApplicationWriteDbContext>()
                .UseNpgsql(SharedFixture.DbContainer.GetConnectionString())
                .AddInterceptors(SoftDeleteInterceptor, AuditingInterceptor)
                .Options;
            _writeDbContext = new ApplicationWriteDbContext(writeOptions);
            _writeRepository = new BrandWriteRepository(_writeDbContext, httpContextAccessor);

            _unitOfWork = new UnitOfWork<ApplicationWriteDbContext>(_writeDbContext);

            _cache = new BrandCache(Cache, _readRepository);
        }

        public override async ValueTask DisposeAsync()
        {
            if (_unitOfWork is IDisposable disposable)
            {
                disposable.Dispose();
            }
            await _writeDbContext.DisposeAsync();
            await base.DisposeAsync();
            GC.SuppressFinalize(this);
        }

        [Fact]
        public async Task GetOrSetByIdAsyncShouldReturnBrandReadModelWhenBrandExists()
        {
            // Arrange - Create brand with write repository
            var brandResult = Brand.Create("Test Brand", "desc", "https://brand.com");
            var brand = brandResult.Value;
            await _writeRepository.AddAsync(brand, TestContext.Current.CancellationToken);
            await _unitOfWork.SaveChangesAsync(TestContext.Current.CancellationToken);

            // Act - Use cache to retrieve brand
            var result = await _cache.GetOrSetByIdAsync(brand.Id, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            result.ShouldNotBeNull();
            result!.Name.ShouldBe("Test Brand");
            result.ShouldBeOfType<BrandReadModel>();
        }

        [Fact]
        public async Task SetAsync_Should_StoreBrandReadModelInCache()
        {
            // Arrange - Create brand with write repository
            var brandResult = Brand.Create("Cache Brand", "desc", "https://brand.com");
            var brand = brandResult.Value;
            await _writeRepository.AddAsync(brand, TestContext.Current.CancellationToken);
            await _unitOfWork.SaveChangesAsync(TestContext.Current.CancellationToken);

            // Get the read model from read repository
            var readModel = await _readRepository.GetByIdAsync(brand.Id, TestContext.Current.CancellationToken);
            readModel.ShouldNotBeNull();

            // Act - Store read model in cache
            await _cache.SetAsync(brand.Id, readModel!, TestContext.Current.CancellationToken);
            var result = await _cache.GetOrSetByIdAsync(brand.Id, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            result.ShouldNotBeNull();
            result!.Name.ShouldBe("Cache Brand");
            result.ShouldBeOfType<BrandReadModel>();
        }

        [Fact]
        public async Task RemoveAsync_Should_RemoveBrandFromCache()
        {
            // Arrange - Create brand with write repository
            var brandResult = Brand.Create("Remove Brand", "desc", "https://brand.com");
            var brand = brandResult.Value;
            await _writeRepository.AddAsync(brand, TestContext.Current.CancellationToken);
            await _unitOfWork.SaveChangesAsync(TestContext.Current.CancellationToken);

            // Get the read model and cache it
            var readModel = await _readRepository.GetByIdAsync(brand.Id, TestContext.Current.CancellationToken);
            readModel.ShouldNotBeNull();
            await _cache.SetAsync(brand.Id, readModel!, TestContext.Current.CancellationToken);

            // Act - Remove from cache then delete from repository
            await _cache.RemoveAsync(brand.Id, TestContext.Current.CancellationToken);
            _writeRepository.Delete(brand);
            await _unitOfWork.SaveChangesAsync(TestContext.Current.CancellationToken);

            // Try to get from cache - should trigger repository lookup which will now return null
            var result = await _cache.TryGetByIdAsync(brand.Id, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            result.ShouldBeNull();
        }

        [Fact]
        public async Task ExpireAsync_Should_ExpireBrandFromCache()
        {
            // Arrange - Create brand with write repository
            var brandResult = Brand.Create("Expire Brand", "desc", "https://brand.com");
            var brand = brandResult.Value;
            await _writeRepository.AddAsync(brand, TestContext.Current.CancellationToken);
            await _unitOfWork.SaveChangesAsync(TestContext.Current.CancellationToken);

            // Get the read model and cache it
            var readModel = await _readRepository.GetByIdAsync(brand.Id, TestContext.Current.CancellationToken);
            readModel.ShouldNotBeNull();
            await _cache.SetAsync(brand.Id, readModel!, TestContext.Current.CancellationToken);

            // Act - Expire in cache then delete from repository
            await _cache.ExpireAsync(brand.Id, TestContext.Current.CancellationToken);
            _writeRepository.Delete(brand);
            await _unitOfWork.SaveChangesAsync(TestContext.Current.CancellationToken);

            // Try to get from cache - should trigger repository lookup which will now return null
            var result = await _cache.TryGetByIdAsync(brand.Id, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            result.ShouldBeNull();
        }

        [Fact]
        public async Task GetOrSetByIdAsync_Should_ReturnNull_When_BrandDoesNotExist()
        {
            // Act
            var result = await _cache.GetOrSetByIdAsync(Guid.NewGuid(), cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            result.ShouldBeNull();
        }

        [Fact]
        public async Task Cache_Should_StoreAndRetrieveReadModels()
        {
            // Arrange - Create brand
            var brandResult = Brand.Create("ReadModel Brand", "Read model test", "https://readmodel.com");
            var brand = brandResult.Value;
            await _writeRepository.AddAsync(brand, TestContext.Current.CancellationToken);
            await _unitOfWork.SaveChangesAsync(TestContext.Current.CancellationToken);

            // Get read model directly
            var directReadModel = await _readRepository.GetByIdAsync(brand.Id, TestContext.Current.CancellationToken);
            directReadModel.ShouldNotBeNull();

            // Act - Cache should store and retrieve read model
            var cachedModel = await _cache.GetOrSetByIdAsync(brand.Id, cancellationToken: TestContext.Current.CancellationToken);

            // Assert - Should be read model, not domain entity
            cachedModel.ShouldNotBeNull();
            cachedModel.ShouldBeOfType<BrandReadModel>();

            // Verify read model properties
            cachedModel!.Id.ShouldBe(brand.Id);
            cachedModel.Name.ShouldBe("ReadModel Brand");
            cachedModel.Description.ShouldBe("Read model test");
            cachedModel.Website.ShouldBe("https://readmodel.com");
        }

        [Fact]
        public async Task CacheShouldReflectChangesWhenEntityIsUpdated()
        {
            // Arrange - Create a brand with write repository
            var brandResult = Brand.Create("Original Brand", "Original description", "https://original.com");
            var brand = brandResult.Value;
            await _writeRepository.AddAsync(brand, TestContext.Current.CancellationToken);
            await _unitOfWork.SaveChangesAsync(TestContext.Current.CancellationToken);

            // Cache the brand
            var originalCachedModel = await _cache.GetOrSetByIdAsync(brand.Id, cancellationToken: TestContext.Current.CancellationToken);
            originalCachedModel.ShouldNotBeNull();
            originalCachedModel!.Name.ShouldBe("Original Brand");

            // Act - Update the brand with write repository
            var updateResult = brand.Update("Updated Brand", "Updated description", "https://updated.com");
            updateResult.IsError.ShouldBeFalse();

            _writeRepository.Update(brand);
            await _unitOfWork.SaveChangesAsync(TestContext.Current.CancellationToken);

            // Expire the cache to ensure it will be refreshed
            await _cache.ExpireAsync(brand.Id, TestContext.Current.CancellationToken);

            // Get the updated brand from cache (should fetch from repository)
            var updatedCachedModel = await _cache.GetOrSetByIdAsync(brand.Id, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            updatedCachedModel.ShouldNotBeNull();
            updatedCachedModel!.Name.ShouldBe("Updated Brand");
            updatedCachedModel.Description.ShouldBe("Updated description");
            updatedCachedModel.Website.ShouldBe("https://updated.com");
        }

        [Fact]
        public async Task CacheShouldReturnNullAfterEntityIsDeleted()
        {
            // Arrange - Create a brand with write repository
            var brandResult = Brand.Create("Brand To Delete", "Will be deleted", "https://delete.com");
            var brand = brandResult.Value;
            await _writeRepository.AddAsync(brand, TestContext.Current.CancellationToken);
            await _unitOfWork.SaveChangesAsync(TestContext.Current.CancellationToken);

            // Cache the brand
            var cachedModel = await _cache.GetOrSetByIdAsync(brand.Id, cancellationToken: TestContext.Current.CancellationToken);
            cachedModel.ShouldNotBeNull();

            // Act - Delete the brand with write repository
            _writeRepository.Delete(brand);
            await _unitOfWork.SaveChangesAsync(TestContext.Current.CancellationToken);

            // Expire the cache to ensure it will be refreshed
            await _cache.RemoveAsync(brand.Id, TestContext.Current.CancellationToken);

            // Try to get the deleted brand from cache (should fetch from repository and return null)
            var deletedCachedModel = await _cache.TryGetByIdAsync(brand.Id, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            deletedCachedModel.ShouldBeNull();
        }
    }
}

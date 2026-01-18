#nullable enable
using Catalog.Application.Products.ReadModels;
using Catalog.Application.Products.Repositories;
using Catalog.Infrastructure.Persistence;
using Catalog.Infrastructure.Persistence.Repositories.Read;
using Catalog.IntegrationTests.Shared;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Catalog.IntegrationTests.Infrastructure.Products
{
    [Collection("SharedTestcontainers")]
    public class ProductReadRepositoryIntegrationTests : BaseReadRepoTestFixture<ApplicationReadDbContext>
    {
        private IProductReadRepository _repository = null!;

        public ProductReadRepositoryIntegrationTests(SharedTestcontainersFixture sharedFixture)
            : base(sharedFixture)
        {
        }

        public override async ValueTask InitializeAsync()
        {
            await base.InitializeAsync();
            _repository = new ProductReadRepository(ReadDbContext);
        }

        protected override ApplicationReadDbContext CreateReadDbContext(DbContextOptions<ApplicationReadDbContext> options)
        {
            return new ApplicationReadDbContext(options);
        }

        public override async ValueTask DisposeAsync()
        {
            await base.DisposeAsync();
            GC.SuppressFinalize(this);
        }

        [Fact]
        public async Task GetByIdAsyncShouldReturnCorrectProductReadModel()
        {
            // Arrange: seed read-models directly into the read DB
            var brandId = Guid.NewGuid();
            var categoryId = Guid.NewGuid();
            var productId = Guid.NewGuid();

            var brandRm = new Catalog.Application.Brands.ReadModels.BrandReadModel { Id = brandId, Name = "TestBrand", Description = "desc", Website = "https://test.com" };
            var categoryRm = new Catalog.Application.Categories.ReadModels.CategoryReadModel { Id = categoryId, Name = "TestCategory", Description = "desc" };
            var productRm = new ProductReadModel
            {
                Id = productId,
                Name = "TestProduct",
                Description = "Test Description",
                Sku = "SKU123",
                BrandId = brandId,
                BrandName = "TestBrand",
                CategoryId = categoryId,
                CategoryName = "TestCategory"
            };

            ReadDbContext.Set<Catalog.Application.Brands.ReadModels.BrandReadModel>().Add(brandRm);
            ReadDbContext.Set<Catalog.Application.Categories.ReadModels.CategoryReadModel>().Add(categoryRm);
            ReadDbContext.Set<ProductReadModel>().Add(productRm);
            await ReadDbContext.SaveChangesAsync(CancellationToken.None);

            // Act
            var readModel = await _repository.GetByIdAsync(productId, CancellationToken.None);

            // Assert
            readModel.ShouldNotBeNull();
            readModel!.Id.ShouldBe(productId);
            readModel.Name.ShouldBe("TestProduct");
            readModel.Description.ShouldBe("Test Description");
            readModel.Sku.ShouldBe("SKU123");
        }

        [Fact]
        public async Task GetAllAsyncShouldReturnAllProducts()
        {
            // Arrange: seed multiple read-models
            var brandId = Guid.NewGuid();
            var categoryId = Guid.NewGuid();
            var list = Enumerable.Range(1, 3).Select(i => new ProductReadModel
            {
                Id = Guid.NewGuid(),
                Name = $"ListTestProduct{i}",
                Description = $"Description {i}",
                Sku = $"SKU{i}",
                BrandId = brandId,
                BrandName = "ListTestBrand",
                CategoryId = categoryId,
                CategoryName = "ListTestCategory"
            }).ToList();

            ReadDbContext.Set<ProductReadModel>().AddRange(list);
            await ReadDbContext.SaveChangesAsync(CancellationToken.None);

            // Act
            var products = await _repository.GetAllAsync(CancellationToken.None);

            // Assert
            products.ShouldNotBeNull();
            products.Count.ShouldBeGreaterThanOrEqualTo(3);
            products.Count(p => p.Name.StartsWith("ListTestProduct", StringComparison.Ordinal)).ShouldBe(3);
        }

        [Fact]
        public async Task GetBySkuAsyncShouldReturnCorrectProduct()
        {
            // Arrange: seed product read-model with unique SKU
            var productId = Guid.NewGuid();
            var productRm = new ProductReadModel
            {
                Id = productId,
                Name = "SkuTestProduct",
                Description = "Test Description",
                Sku = "UNIQUE_SKU_123"
            };
            ReadDbContext.Set<ProductReadModel>().Add(productRm);
            await ReadDbContext.SaveChangesAsync(CancellationToken.None);

            // Act
            var readModel = await _repository.GetBySkuAsync("UNIQUE_SKU_123", CancellationToken.None);

            // Assert
            readModel.ShouldNotBeNull();
            readModel!.Name.ShouldBe("SkuTestProduct");
            readModel.Sku.ShouldBe("UNIQUE_SKU_123");
        }


    }
}

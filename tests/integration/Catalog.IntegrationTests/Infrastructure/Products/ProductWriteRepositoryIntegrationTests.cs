#nullable enable
using Catalog.Domain.Entities.BrandAggregate;
using Catalog.Domain.Entities.BrandAggregate.Repositories;
using Catalog.Domain.Entities.CategoryAggregate;
using Catalog.Domain.Entities.CategoryAggregate.Repositories;
using Catalog.Domain.Entities.ProductAggregate;
using Catalog.Domain.Entities.ProductAggregate.Repositories;
using Catalog.Domain.Entities.ProductAggregate.Specifications;
using Catalog.Infrastructure.Persistence;
using Catalog.Infrastructure.Persistence.Repositories.Write;
using Catalog.IntegrationTests.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Core.Database;
using SharedKernel.Persistence.Database.EFCore;
using Shouldly;

namespace Catalog.IntegrationTests.Infrastructure.Products
{
    [Collection("SharedTestcontainers")]
    public class ProductWriteRepositoryIntegrationTests : BaseWriteRepoTestFixture<ApplicationWriteDbContext, IUnitOfWork>
    {
        private IProductWriteRepository _repository = null!;
        private IBrandWriteRepository _brandRepository = null!;
        private ICategoryWriteRepository _categoryRepository = null!;

        public ProductWriteRepositoryIntegrationTests(SharedTestcontainersFixture sharedFixture)
            : base(sharedFixture)
        {
        }

        public override async ValueTask InitializeAsync()
        {
            await base.InitializeAsync();
            var httpContextAccessor = new HttpContextAccessor();
            _repository = new ProductWriteRepository(WriteDbContext, httpContextAccessor);
            _brandRepository = new BrandWriteRepository(WriteDbContext, httpContextAccessor);
            _categoryRepository = new CategoryWriteRepository(WriteDbContext, httpContextAccessor);
        }

        protected override ApplicationWriteDbContext CreateWriteDbContext(DbContextOptions<ApplicationWriteDbContext> options)
        {
            return new ApplicationWriteDbContext(options);
        }

        protected override IUnitOfWork CreateUnitOfWork(ApplicationWriteDbContext context)
        {
            return new UnitOfWork<ApplicationWriteDbContext>(context);
        }

        [Fact]
        public async Task AddAndFindByIdShouldWorkCorrectly()
        {
            // Arrange
            var brandResult = Brand.Create("TestBrand", "desc", "https://test.com");
            var brand = brandResult.Value;
            await _brandRepository.AddAsync(brand, CancellationToken.None);

            var categoryResult = Category.Create("TestCategory", "desc");
            var category = categoryResult.Value;
            await _categoryRepository.AddAsync(category, CancellationToken.None);
            await UnitOfWork.SaveChangesAsync(CancellationToken.None);

            var productResult = Product.Create(
                "TestProduct",
                "Test Description",
                "SKU123",
                "GTIN123",
                new List<Category> { category },
                true,
                brand.Id
            );
            var product = productResult.Value;

            // Act
            await _repository.AddAsync(product, CancellationToken.None);
            await UnitOfWork.SaveChangesAsync(CancellationToken.None);

            var specification = new ProductByIdSpecification(product.Id, includeRelations: true);
            var result = await _repository.FirstOrDefaultAsync(specification, CancellationToken.None);

            // Assert
            result.ShouldNotBeNull();
            result!.Id.ShouldBe(product.Id);
            result.Name.ShouldBe("TestProduct");
            result.Description.ShouldBe("Test Description");
            result.ProductSKU.ShouldBe("SKU123");
            result.Brand.ShouldNotBeNull();
            result.Brand!.Name.ShouldBe("TestBrand");
            result.Categories.ShouldNotBeNull();
            result.Categories.Count.ShouldBe(1);
            result.Categories.First().Name.ShouldBe("TestCategory");
        }

        [Fact]
        public async Task UpdateShouldPersistChanges()
        {
            // Arrange
            var brandResult = Brand.Create("UpdateTestBrand", "desc", "https://test.com");
            var brand = brandResult.Value;
            await _brandRepository.AddAsync(brand, CancellationToken.None);

            var categoryResult = Category.Create("UpdateTestCategory", "desc");
            var category = categoryResult.Value;
            await _categoryRepository.AddAsync(category, CancellationToken.None);
            await UnitOfWork.SaveChangesAsync(CancellationToken.None);

            var productResult = Product.Create(
                "OriginalName",
                "Original description",
                "OriginalSKU",
                "OriginalGTIN",
                new List<Category> { category },
                true,
                brand.Id
            );
            var product = productResult.Value;
            await _repository.AddAsync(product, CancellationToken.None);
            await UnitOfWork.SaveChangesAsync(CancellationToken.None);

            // Act
            var updateResult = product.Update("UpdatedName");
            updateResult.IsError.ShouldBeFalse();

            _repository.Update(product);
            await UnitOfWork.SaveChangesAsync(CancellationToken.None);

            var specification = new ProductByIdSpecification(product.Id);
            var updatedProduct = await _repository.FirstOrDefaultAsync(specification, CancellationToken.None);

            // Assert
            updatedProduct.ShouldNotBeNull();
            updatedProduct!.Name.ShouldBe("UpdatedName");
        }

        [Fact]
        public async Task DeleteShouldRemoveProduct()
        {
            // Arrange
            var brandResult = Brand.Create("DeleteTestBrand", "desc", "https://test.com");
            var brand = brandResult.Value;
            await _brandRepository.AddAsync(brand, CancellationToken.None);

            var categoryResult = Category.Create("DeleteTestCategory", "desc");
            var category = categoryResult.Value;
            await _categoryRepository.AddAsync(category, CancellationToken.None);
            await UnitOfWork.SaveChangesAsync(CancellationToken.None);

            var productResult = Product.Create(
                "ProductToDelete",
                "Description to delete",
                "DeleteSKU",
                "DeleteGTIN",
                new List<Category> { category },
                true,
                brand.Id
            );
            var product = productResult.Value;
            await _repository.AddAsync(product, CancellationToken.None);
            await UnitOfWork.SaveChangesAsync(CancellationToken.None);

            // Act
            _repository.Delete(product);
            await UnitOfWork.SaveChangesAsync(CancellationToken.None);

            var specification = new ProductByIdSpecification(product.Id);
            var deletedProduct = await _repository.FirstOrDefaultAsync(specification, CancellationToken.None);

            // Assert
            deletedProduct.ShouldBeNull();
        }

        [Fact]
        public async Task FindBySkuShouldReturnCorrectProduct()
        {
            // Arrange
            var brandResult = Brand.Create("SkuFindTestBrand", "desc", "https://test.com");
            var brand = brandResult.Value;
            await _brandRepository.AddAsync(brand, CancellationToken.None);

            var categoryResult = Category.Create("SkuFindTestCategory", "desc");
            var category = categoryResult.Value;
            await _categoryRepository.AddAsync(category, CancellationToken.None);
            await UnitOfWork.SaveChangesAsync(CancellationToken.None);

            var uniqueSku = $"UNIQUE_SKU_{Guid.NewGuid()}";
            var productResult = Product.Create(
                "SkuFindTestProduct",
                "Description for SKU Find",
                uniqueSku,
                "FindGTIN",
                new List<Category> { category },
                true,
                brand.Id
            );
            var product = productResult.Value;
            await _repository.AddAsync(product, CancellationToken.None);
            await UnitOfWork.SaveChangesAsync(CancellationToken.None);

            // Act
            var result = await _repository.GetBySkuAsync(uniqueSku, CancellationToken.None);

            // Assert
            result.ShouldNotBeNull();
            result!.ProductSKU.ShouldBe(uniqueSku);
            result.Name.ShouldBe("SkuFindTestProduct");
        }
    }
}

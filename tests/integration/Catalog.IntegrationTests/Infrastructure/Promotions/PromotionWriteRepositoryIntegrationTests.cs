#nullable enable
using Catalog.Domain.Entities.BrandAggregate;
using Catalog.Domain.Entities.BrandAggregate.Repositories;
using Catalog.Domain.Entities.CategoryAggregate;
using Catalog.Domain.Entities.CategoryAggregate.Repositories;
using Catalog.Domain.Entities.ProductAggregate;
using Catalog.Domain.Entities.ProductAggregate.Repositories;
using Catalog.Domain.Entities.PromotionAggregate;
using Catalog.Domain.Entities.PromotionAggregate.Repositories;
using Catalog.Domain.Entities.PromotionAggregate.Specifications;
using Catalog.Infrastructure.Persistence;
using Catalog.Infrastructure.Persistence.Repositories.Write;
using Catalog.IntegrationTests.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Core.Database;
using SharedKernel.Persistence.Database.EFCore;
using Shouldly;

namespace Catalog.IntegrationTests.Infrastructure.Promotions
{
    [Collection("SharedTestcontainers")]
    public class PromotionWriteRepositoryIntegrationTests : BaseWriteRepoTestFixture<ApplicationWriteDbContext, IUnitOfWork>
    {
        private IPromotionWriteRepository _writeRepository = null!;
        private IProductWriteRepository _productWriteRepository = null!;
        private IBrandWriteRepository _brandWriteRepository = null!;
        private ICategoryWriteRepository _categoryWriteRepository = null!;

        public PromotionWriteRepositoryIntegrationTests(SharedTestcontainersFixture sharedFixture) : base(sharedFixture)
        {
        }

        protected override ApplicationWriteDbContext CreateWriteDbContext(DbContextOptions<ApplicationWriteDbContext> options)
            => new ApplicationWriteDbContext(options);

        protected override IUnitOfWork CreateUnitOfWork(ApplicationWriteDbContext context)
            => new UnitOfWork<ApplicationWriteDbContext>(context);

        public override async ValueTask InitializeAsync()
        {
            await base.InitializeAsync();

            var httpContextAccessor = new HttpContextAccessor();
            _writeRepository = new PromotionWriteRepository(WriteDbContext, httpContextAccessor);
            _productWriteRepository = new ProductWriteRepository(WriteDbContext, httpContextAccessor);
            _brandWriteRepository = new BrandWriteRepository(WriteDbContext, httpContextAccessor);
            _categoryWriteRepository = new CategoryWriteRepository(WriteDbContext, httpContextAccessor);
        }

        [Fact]
        public async Task AddAndGetPromotion_Works()
        {
            // Arrange: create and persist a brand, category, and product first
            var brandResult = Brand.Create("TestBrand", "desc", "https://test.com");
            var brand = brandResult.Value;
            await _brandWriteRepository.AddAsync(brand, CancellationToken.None);

            var categoryResult = Category.Create("TestCategory", "desc");
            var category = categoryResult.Value;
            await _categoryWriteRepository.AddAsync(category, CancellationToken.None);

            await UnitOfWork.SaveChangesAsync(CancellationToken.None);

            var productResult = Product.Create(
                "TestProduct",
                "desc",
                "SKU123",
                "GTIN123",
                new List<Category> { category },
                true,
                brand.Id
            );
            var product = productResult.Value;
            await _productWriteRepository.AddAsync(product, CancellationToken.None);
            await UnitOfWork.SaveChangesAsync(CancellationToken.None);

            // Act: create promotion
            var promoResult = Promotion.Create(
                "TestPromo",
                "desc",
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow.AddDays(10),
                new List<Product> { product }
            );
            var promo = promoResult.Value;
            await _writeRepository.AddAsync(promo, CancellationToken.None);
            await UnitOfWork.SaveChangesAsync(CancellationToken.None);

            var promoByIdSpec = new PromotionByIdSpecification(promo.Id, includeProducts: true);
            var fetchedFromWrite = await _writeRepository.FirstOrDefaultAsync(promoByIdSpec, CancellationToken.None);

            // Assert
            fetchedFromWrite.ShouldNotBeNull();
            fetchedFromWrite!.Name.ShouldBe("TestPromo");
            fetchedFromWrite.Products.ShouldContain(p => p.Id == product.Id);
        }

        [Fact]
        public async Task Update_Should_PersistChanges()
        {
            // Arrange: create and persist a brand, category, and product first
            var brandResult = Brand.Create("TestBrandForUpdate", "desc", "https://test.com");
            var brand = brandResult.Value;
            await _brandWriteRepository.AddAsync(brand, CancellationToken.None);

            var categoryResult = Category.Create("TestCategoryForUpdate", "desc");
            var category = categoryResult.Value;
            await _categoryWriteRepository.AddAsync(category, CancellationToken.None);

            await UnitOfWork.SaveChangesAsync(CancellationToken.None);

            var productResult = Product.Create(
                "TestProductForUpdate",
                "desc",
                "SKU-UPDATE",
                "GTIN-UPDATE",
                new List<Category> { category },
                true,
                brand.Id
            );
            var product = productResult.Value;
            await _productWriteRepository.AddAsync(product, CancellationToken.None);
            await UnitOfWork.SaveChangesAsync(CancellationToken.None);

            var promoResult = Promotion.Create(
                "OriginalPromo",
                "Original description",
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow.AddDays(5),
                new List<Product> { product }
            );
            var promo = promoResult.Value;
            await _writeRepository.AddAsync(promo, CancellationToken.None);
            await UnitOfWork.SaveChangesAsync(CancellationToken.None);

            var updateResult = promo.Update(
                "UpdatedPromo",
                "Updated description",
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow.AddDays(10),
                new List<Product> { product }
            );
            updateResult.IsError.ShouldBeFalse();

            _writeRepository.Update(promo);
            await UnitOfWork.SaveChangesAsync(CancellationToken.None);

            var promoByIdSpec = new PromotionByIdSpecification(promo.Id);
            var updatedPromo = await _writeRepository.FirstOrDefaultAsync(promoByIdSpec, CancellationToken.None);

            updatedPromo.ShouldNotBeNull();
            updatedPromo!.Name.ShouldBe("UpdatedPromo");
            updatedPromo.Description.ShouldBe("Updated description");
        }

        [Fact]
        public async Task GetByName_Should_ReturnPromotion()
        {
            // Arrange: create and persist a brand, category, and product first
            var brandResult = Brand.Create("TestBrandForGetByName", "desc", "https://test.com");
            var brand = brandResult.Value;
            await _brandWriteRepository.AddAsync(brand, CancellationToken.None);

            var categoryResult = Category.Create("TestCategoryForGetByName", "desc");
            var category = categoryResult.Value;
            await _categoryWriteRepository.AddAsync(category, CancellationToken.None);

            await UnitOfWork.SaveChangesAsync(CancellationToken.None);

            var productResult = Product.Create(
                "TestProductForGetByName",
                "desc",
                "SKU-GETBY",
                "GTIN-GETBY",
                new List<Category> { category },
                true,
                brand.Id
            );
            var product = productResult.Value;
            await _productWriteRepository.AddAsync(product, CancellationToken.None);
            await UnitOfWork.SaveChangesAsync(CancellationToken.None);

            var promoResult = Promotion.Create(
                "NameSearchTest",
                "Description for name search test",
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow.AddDays(7),
                new List<Product> { product }
            );
            var promo = promoResult.Value;
            await _writeRepository.AddAsync(promo, CancellationToken.None);
            await UnitOfWork.SaveChangesAsync(CancellationToken.None);

            var foundByName = await _writeRepository.GetByNameAsync("NameSearchTest", CancellationToken.None);

            foundByName.ShouldNotBeNull();
            foundByName!.Name.ShouldBe("NameSearchTest");
            foundByName.Description.ShouldBe("Description for name search test");
        }

        [Fact]
        public async Task GetActivePromotions_Should_ReturnOnlyActivePromotions()
        {
            var now = DateTimeOffset.UtcNow;
            // Arrange: create and persist a brand, category, and product first
            var brandResult = Brand.Create("TestBrandForActive", "desc", "https://test.com");
            var brand = brandResult.Value;
            await _brandWriteRepository.AddAsync(brand, CancellationToken.None);

            var categoryResult = Category.Create("TestCategoryForActive", "desc");
            var category = categoryResult.Value;
            await _categoryWriteRepository.AddAsync(category, CancellationToken.None);

            await UnitOfWork.SaveChangesAsync(CancellationToken.None);

            var productResult = Product.Create(
                "TestProductForActive",
                "desc",
                "SKU-ACTIVE",
                "GTIN-ACTIVE",
                new List<Category> { category },
                true,
                brand.Id
            );
            var product = productResult.Value;
            await _productWriteRepository.AddAsync(product, CancellationToken.None);

            await UnitOfWork.SaveChangesAsync(CancellationToken.None);

            var activePromoResult = Promotion.Create(
                "ActivePromo",
                "Active promotion",
                now.AddDays(-1),
                now.AddDays(1),
                new List<Product> { product }
            );
            await _writeRepository.AddAsync(activePromoResult.Value, CancellationToken.None);

            var futurePromoResult = Promotion.Create(
                "FuturePromo",
                "Future promotion",
                now.AddDays(1),
                now.AddDays(2),
                new List<Product> { product }
            );
            await _writeRepository.AddAsync(futurePromoResult.Value, CancellationToken.None);

            var pastPromoResult = Promotion.Create(
                "PastPromo",
                "Past promotion",
                now.AddDays(-3),
                now.AddDays(-2),
                new List<Product> { product }
            );
            await _writeRepository.AddAsync(pastPromoResult.Value, CancellationToken.None);

            await UnitOfWork.SaveChangesAsync(CancellationToken.None);

            var activePromotions = await _writeRepository.GetActivePromotionsAsync(CancellationToken.None);

            activePromotions.ShouldNotBeNull();
            activePromotions.Count.ShouldBe(1);
            activePromotions.ShouldContain(p => p.Name == "ActivePromo");
            activePromotions.ShouldNotContain(p => p.Name == "FuturePromo");
            activePromotions.ShouldNotContain(p => p.Name == "PastPromo");
        }
    }
}

#nullable enable
using Catalog.Application.ProductPriceTypes.Repositories;
using Catalog.Infrastructure.Persistence;
using Catalog.Infrastructure.Persistence.Repositories.Read;
using Catalog.IntegrationTests.Shared;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Catalog.Application.ProductPriceTypes.ReadModels;

namespace Catalog.IntegrationTests.Infrastructure.ProductPriceTypes
{
    [Collection("SharedTestcontainers")]
    public class ProductPriceTypeReadRepositoryIntegrationTests : BaseReadRepoTestFixture<ApplicationReadDbContext>
    {
        private IProductPriceTypeReadRepository _readRepository = null!;

        public ProductPriceTypeReadRepositoryIntegrationTests(SharedTestcontainersFixture sharedFixture)
            : base(sharedFixture)
        {
        }

        protected override ApplicationReadDbContext CreateReadDbContext(DbContextOptions<ApplicationReadDbContext> options)
        {
            return new ApplicationReadDbContext(options);
        }

        public override async ValueTask InitializeAsync()
        {
            await base.InitializeAsync();
            _readRepository = new ProductPriceTypeReadRepository(ReadDbContext);
        }

        public override async ValueTask DisposeAsync()
        {
            await base.DisposeAsync();
            GC.SuppressFinalize(this);
        }

        [Fact]
        public async Task GetAllProductPriceTypes_Should_ReturnAllTypes()
        {
            // Arrange: seed multiple read-models directly into the read DB
            var r1 = new ProductPriceTypeReadModel { Id = Guid.NewGuid(), Name = "PriceType1", Description = null };
            var r2 = new ProductPriceTypeReadModel { Id = Guid.NewGuid(), Name = "PriceType2", Description = null };
            ReadDbContext.Set<ProductPriceTypeReadModel>().AddRange(r1, r2);
            await ReadDbContext.SaveChangesAsync(CancellationToken.None);

            // Act
            var allPriceTypes = await _readRepository.GetAllAsync(CancellationToken.None);

            // Assert
            allPriceTypes.ShouldNotBeNull();
            allPriceTypes.Count.ShouldBeGreaterThanOrEqualTo(2);
            allPriceTypes.ShouldContain(p => p.Name == "PriceType1");
            allPriceTypes.ShouldContain(p => p.Name == "PriceType2");
        }
    }
}

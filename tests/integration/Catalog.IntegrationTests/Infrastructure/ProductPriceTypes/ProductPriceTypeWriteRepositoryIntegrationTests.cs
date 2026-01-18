#nullable enable
using Catalog.Domain.Entities.ProductPriceTypeAggregate;
using Catalog.Domain.Entities.ProductPriceTypeAggregate.Repositories;
using Catalog.Domain.Entities.ProductPriceTypeAggregate.Specifications;
using Catalog.Infrastructure.Persistence;
using Catalog.Infrastructure.Persistence.Repositories.Write;
using Catalog.IntegrationTests.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Core.Database;
using SharedKernel.Persistence.Database.EFCore;
using Shouldly;

namespace Catalog.IntegrationTests.Infrastructure.ProductPriceTypes
{
    [Collection("SharedTestcontainers")]
    public class ProductPriceTypeWriteRepositoryIntegrationTests : BaseWriteRepoTestFixture<ApplicationWriteDbContext, IUnitOfWork>
    {
        private IProductPriceTypeWriteRepository _writeRepository = null!;

        public ProductPriceTypeWriteRepositoryIntegrationTests(SharedTestcontainersFixture sharedFixture)
            : base(sharedFixture)
        {
        }

        public override async ValueTask InitializeAsync()
        {
            await base.InitializeAsync();
            var httpContextAccessor = new HttpContextAccessor();
            _writeRepository = new ProductPriceTypeWriteRepository(WriteDbContext, httpContextAccessor);
        }

        [Fact]
        public async Task AddAndGetProductPriceType_Works()
        {
            // Arrange
            var priceTypeResult = ProductPriceType.Create("Retail", 1);
            var priceType = priceTypeResult.Value;

            // Act
            await _writeRepository.AddAsync(priceType, CancellationToken.None);
            await UnitOfWork.SaveChangesAsync(CancellationToken.None);

            var priceTypeByIdSpec = new ProductPriceTypeByIdSpecification(priceType.Id);
            var fetchedFromWrite = await _writeRepository.FirstOrDefaultAsync(priceTypeByIdSpec, CancellationToken.None);

            // Assert
            fetchedFromWrite.ShouldNotBeNull();
            fetchedFromWrite!.Name.ShouldBe("Retail");
            fetchedFromWrite.Priority.ShouldBe(1);
        }

        [Fact]
        public async Task Update_Should_PersistChanges()
        {
            // Arrange
            var priceTypeResult = ProductPriceType.Create("OriginalName", 5);
            var priceType = priceTypeResult.Value;
            await _writeRepository.AddAsync(priceType, CancellationToken.None);
            await UnitOfWork.SaveChangesAsync(CancellationToken.None);

            // Act
            var updateResult = priceType.Update("UpdatedName", 10);
            updateResult.IsError.ShouldBeFalse();

            _writeRepository.Update(priceType);
            await UnitOfWork.SaveChangesAsync(CancellationToken.None);

            var priceTypeByIdSpec = new ProductPriceTypeByIdSpecification(priceType.Id);
            var updatedPriceType = await _writeRepository.FirstOrDefaultAsync(priceTypeByIdSpec, CancellationToken.None);

            // Assert
            updatedPriceType.ShouldNotBeNull();
            updatedPriceType!.Name.ShouldBe("UpdatedName");
            updatedPriceType.Priority.ShouldBe(10);
        }

        protected override ApplicationWriteDbContext CreateWriteDbContext(DbContextOptions<ApplicationWriteDbContext> options)
        {
            return new ApplicationWriteDbContext(options);
        }

        protected override IUnitOfWork CreateUnitOfWork(ApplicationWriteDbContext context)
        {
            return new UnitOfWork<ApplicationWriteDbContext>(context);
        }
    }
}

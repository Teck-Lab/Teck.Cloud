#nullable enable
using Catalog.Domain.Entities.SupplierAggregate;
using Catalog.Domain.Entities.SupplierAggregate.Repositories;
using Catalog.Domain.Entities.SupplierAggregate.Specifications;
using Catalog.Infrastructure.Persistence;
using Catalog.Infrastructure.Persistence.Repositories.Write;
using Catalog.IntegrationTests.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Core.Database;
using SharedKernel.Persistence.Database.EFCore;
using Shouldly;

namespace Catalog.IntegrationTests.Infrastructure.Suppliers
{
    [Collection("SharedTestcontainers")]
    public class SupplierWriteRepositoryIntegrationTests : BaseWriteRepoTestFixture<ApplicationWriteDbContext, IUnitOfWork>
    {
        private ISupplierWriteRepository _writeRepository = null!;
        public SupplierWriteRepositoryIntegrationTests(SharedTestcontainersFixture sharedFixture) : base(sharedFixture)
        {
        }

        protected override ApplicationWriteDbContext CreateWriteDbContext(DbContextOptions<ApplicationWriteDbContext> options)
        {
            return new ApplicationWriteDbContext(options);
        }

        protected override IUnitOfWork CreateUnitOfWork(ApplicationWriteDbContext dbContext)
        {
            return new UnitOfWork<ApplicationWriteDbContext>(dbContext);
        }

        public override async ValueTask InitializeAsync()
        {
            await base.InitializeAsync();
            var httpContextAccessor = new HttpContextAccessor();
            _writeRepository = new SupplierWriteRepository(WriteDbContext!, httpContextAccessor);
        }

        public override async ValueTask DisposeAsync()
        {
            await base.DisposeAsync();
            GC.SuppressFinalize(this);
        }

        [Fact]
        public async Task AddAndGetSupplier_Works()
        {
            var supplierResult = Supplier.Create("TestSupplier", "desc", "https://supplier.com");
            var supplier = supplierResult.Value;

            await _writeRepository.AddAsync(supplier, CancellationToken.None);
            await UnitOfWork.SaveChangesAsync(CancellationToken.None);

            var supplierByIdSpec = new SupplierByIdSpecification(supplier.Id);
            var fetchedFromWrite = await _writeRepository.FirstOrDefaultAsync(supplierByIdSpec, CancellationToken.None);

            fetchedFromWrite.ShouldNotBeNull();
            fetchedFromWrite!.Name.ShouldBe("TestSupplier");
            fetchedFromWrite.Website.ShouldBe("https://supplier.com");
        }

        [Fact]
        public async Task Update_Should_PersistChanges()
        {
            var supplierResult = Supplier.Create("OriginalName", "Original description", "https://original.com");
            var supplier = supplierResult.Value;
            await _writeRepository.AddAsync(supplier, CancellationToken.None);
            await UnitOfWork.SaveChangesAsync(CancellationToken.None);

            var updateResult = supplier.Update("UpdatedName", "Updated description", "https://updated.com");
            updateResult.IsError.ShouldBeFalse();

            _writeRepository.Update(supplier);
            await UnitOfWork.SaveChangesAsync(CancellationToken.None);

            var supplierByIdSpec = new SupplierByIdSpecification(supplier.Id);
            var updatedSupplier = await _writeRepository.FirstOrDefaultAsync(supplierByIdSpec, CancellationToken.None);

            updatedSupplier.ShouldNotBeNull();
            updatedSupplier!.Name.ShouldBe("UpdatedName");
            updatedSupplier.Description.ShouldBe("Updated description");
            updatedSupplier.Website.ShouldBe("https://updated.com");
        }

        [Fact]
        public async Task GetByName_Should_ReturnSupplier()
        {
            var supplierResult = Supplier.Create("NameSearchTest", "Description for name search test", "https://name-search.com");
            var supplier = supplierResult.Value;
            await _writeRepository.AddAsync(supplier, CancellationToken.None);
            await UnitOfWork.SaveChangesAsync(CancellationToken.None);

            var foundByName = await _writeRepository.GetByNameAsync("NameSearchTest", CancellationToken.None);

            foundByName.ShouldNotBeNull();
            foundByName!.Name.ShouldBe("NameSearchTest");
            foundByName.Description.ShouldBe("Description for name search test");
            foundByName.Website.ShouldBe("https://name-search.com");
        }

        [Fact]
        public async Task GetAllSuppliers_Should_ReturnAllSuppliers()
        {
            var supplier1Result = Supplier.Create("Supplier1", "Description 1", "https://supplier1.com");
            var supplier2Result = Supplier.Create("Supplier2", "Description 2", "https://supplier2.com");

            await _writeRepository.AddAsync(supplier1Result.Value, CancellationToken.None);
            await _writeRepository.AddAsync(supplier2Result.Value, CancellationToken.None);
            await UnitOfWork.SaveChangesAsync(CancellationToken.None);

            var found1 = await _writeRepository.GetByNameAsync("Supplier1", CancellationToken.None);
            var found2 = await _writeRepository.GetByNameAsync("Supplier2", CancellationToken.None);

            found1.ShouldNotBeNull();
            found2.ShouldNotBeNull();
        }
    }
}

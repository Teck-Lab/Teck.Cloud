#nullable enable
using Catalog.Application.Suppliers.ReadModels;
using Catalog.Application.Suppliers.Repositories;
using Catalog.Infrastructure.Persistence;
using Catalog.Infrastructure.Persistence.Repositories.Read;
using Catalog.IntegrationTests.Shared;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Catalog.IntegrationTests.Infrastructure.Suppliers
{
    [Collection("SharedTestcontainers")]
    public class SupplierReadRepositoryIntegrationTests : BaseReadRepoTestFixture<ApplicationReadDbContext>
    {
        private ISupplierReadRepository _repository = null!;

        public SupplierReadRepositoryIntegrationTests(SharedTestcontainersFixture sharedFixture)
            : base(sharedFixture)
        {
        }

        public override async ValueTask InitializeAsync()
        {
            await base.InitializeAsync();
            _repository = new SupplierReadRepository(ReadDbContext!);
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
        public async Task GetByIdAsyncShouldReturnCorrectSupplierReadModel()
        {
            // Arrange: seed read-model directly into the read DB
            var supplierId = Guid.NewGuid();
            var supplierRm = new SupplierReadModel
            {
                Id = supplierId,
                Name = "TestSupplier",
                Description = "desc",
                WebsiteUrl = new Uri("https://test.com")
            };

            ReadDbContext.Set<SupplierReadModel>().Add(supplierRm);
            await ReadDbContext.SaveChangesAsync(CancellationToken.None);

            // Act
            var readModel = await _repository.GetByIdAsync(supplierId, CancellationToken.None);

            // Assert
            readModel.ShouldNotBeNull();
            readModel!.Id.ShouldBe(supplierId);
            readModel.Name.ShouldBe("TestSupplier");
        }

        [Fact]
        public async Task GetAllAsyncShouldReturnAllSuppliers()
        {
            // Arrange: seed multiple read-models
            var list = Enumerable.Range(1, 2).Select(i => new SupplierReadModel
            {
                Id = Guid.NewGuid(),
                Name = $"ListTestSupplier{i}",
                Description = $"Description {i}",
                WebsiteUrl = new Uri($"https://supplier{i}.com")
            }).ToList();

            ReadDbContext.Set<SupplierReadModel>().AddRange(list);
            await ReadDbContext.SaveChangesAsync(CancellationToken.None);

            // Act
            var suppliers = await _repository.GetAllAsync(CancellationToken.None);

            // Assert
            suppliers.ShouldNotBeNull();
            suppliers.Count.ShouldBeGreaterThanOrEqualTo(2);
            suppliers.ShouldContain(s => s.Name.StartsWith("ListTestSupplier"));
        }
    }
}

#nullable enable
using Catalog.Application.Categories.ReadModels;
using Catalog.Application.Categories.Repositories;
using Catalog.Infrastructure.Persistence;
using Catalog.Infrastructure.Persistence.Repositories.Read;
using Catalog.IntegrationTests.Shared;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Catalog.IntegrationTests.Infrastructure.Categories
{
    [Collection("SharedTestcontainers")]
    public class CategoryReadRepositoryIntegrationTests : BaseReadRepoTestFixture<ApplicationReadDbContext>
    {
        private ICategoryReadRepository _readRepository = null!;

        public CategoryReadRepositoryIntegrationTests(SharedTestcontainersFixture sharedFixture)
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
            _readRepository = new CategoryReadRepository(ReadDbContext);
        }

        public override async ValueTask DisposeAsync()
        {
            await base.DisposeAsync();
            GC.SuppressFinalize(this);
        }

        [Fact]
        public async Task GetById_ReadModel_ReturnsCategory()
        {
            // Arrange: seed a read-model directly into the read database
            var id = Guid.NewGuid();
            var readModel = new CategoryReadModel
            {
                Id = id,
                Name = "ReadOnlyCategory",
                Description = "Description for read-only test",
                ParentId = null,
                ParentName = null
            };

            // Seed directly into the read DB for read-only test
            ReadDbContext.Set<CategoryReadModel>().Add(readModel);
            await ReadDbContext.SaveChangesAsync(CancellationToken.None);

            // Act
            var fetched = await _readRepository.GetByIdAsync(id, CancellationToken.None);

            // Assert
            fetched.ShouldNotBeNull();
            fetched!.Name.ShouldBe("ReadOnlyCategory");
            fetched.Description.ShouldBe("Description for read-only test");
        }

        [Fact]
        public async Task GetAllCategories_Should_ReturnAllCategories()
        {
            // Arrange: seed multiple read-models directly into the read DB
            var r1 = new CategoryReadModel { Id = Guid.NewGuid(), Name = "Category1", Description = "Description 1" };
            var r2 = new CategoryReadModel { Id = Guid.NewGuid(), Name = "Category2", Description = "Description 2" };
            ReadDbContext.Set<CategoryReadModel>().AddRange(r1, r2);
            await ReadDbContext.SaveChangesAsync(CancellationToken.None);

            // Act
            var allCategories = await _readRepository.GetAllAsync(CancellationToken.None);

            // Assert
            allCategories.ShouldNotBeNull();
            allCategories.Count.ShouldBeGreaterThanOrEqualTo(2);
            allCategories.ShouldContain(c => c.Name == "Category1");
            allCategories.ShouldContain(c => c.Name == "Category2");
        }
    }
}

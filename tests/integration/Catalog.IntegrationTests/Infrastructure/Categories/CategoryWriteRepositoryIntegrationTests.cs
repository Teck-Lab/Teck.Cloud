#nullable enable
using Catalog.Domain.Entities.CategoryAggregate;
using Catalog.Domain.Entities.CategoryAggregate.Repositories;
using Catalog.Domain.Entities.CategoryAggregate.Specifications;
using Catalog.Infrastructure.Persistence;
using Catalog.Infrastructure.Persistence.Repositories.Write;
using Catalog.IntegrationTests.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Core.Database;
using SharedKernel.Persistence.Database.EFCore;
using Shouldly;

namespace Catalog.IntegrationTests.Infrastructure.Categories
{
    [Collection("SharedTestcontainers")]
    public class CategoryWriteRepositoryIntegrationTests : BaseWriteRepoTestFixture<ApplicationWriteDbContext, IUnitOfWork>
    {
        private ICategoryWriteRepository _writeRepository = null!;

        public CategoryWriteRepositoryIntegrationTests(SharedTestcontainersFixture sharedFixture)
            : base(sharedFixture)
        {
        }

        protected override ApplicationWriteDbContext CreateWriteDbContext(DbContextOptions<ApplicationWriteDbContext> options)
        {
            return new ApplicationWriteDbContext(options);
        }

        protected override IUnitOfWork CreateUnitOfWork(ApplicationWriteDbContext context)
        {
            return new UnitOfWork<ApplicationWriteDbContext>(context);
        }

        public override async ValueTask InitializeAsync()
        {
            await base.InitializeAsync();
            var httpContextAccessor = new HttpContextAccessor();
            _writeRepository = new CategoryWriteRepository(WriteDbContext, httpContextAccessor);
        }

        [Fact]
        public async Task AddAndGetCategory_Works()
        {
            // Arrange
            var categoryResult = Category.Create("TestCategory", "desc");
            var category = categoryResult.Value;

            // Act
            await _writeRepository.AddAsync(category, CancellationToken.None);
            await UnitOfWork.SaveChangesAsync(CancellationToken.None);

            var categoryByIdSpec = new CategoryByIdSpecification(category.Id);
            var fetchedFromWrite = await _writeRepository.FirstOrDefaultAsync(categoryByIdSpec, CancellationToken.None);

            // Assert
            fetchedFromWrite.ShouldNotBeNull();
            fetchedFromWrite!.Name.ShouldBe("TestCategory");
        }

        [Fact]
        public async Task Update_Should_PersistChanges()
        {
            // Arrange
            var categoryResult = Category.Create("OriginalName", "Original description");
            var category = categoryResult.Value;
            await _writeRepository.AddAsync(category, CancellationToken.None);
            await UnitOfWork.SaveChangesAsync(CancellationToken.None);

            // Act
            var updateResult = category.Update("UpdatedName", "Updated description");
            updateResult.IsError.ShouldBeFalse();

            _writeRepository.Update(category);
            await UnitOfWork.SaveChangesAsync(CancellationToken.None);

            var categoryByIdSpec = new CategoryByIdSpecification(category.Id);
            var updatedCategory = await _writeRepository.FirstOrDefaultAsync(categoryByIdSpec, CancellationToken.None);

            // Assert
            updatedCategory.ShouldNotBeNull();
            updatedCategory!.Name.ShouldBe("UpdatedName");
            updatedCategory.Description.ShouldBe("Updated description");
        }

        [Fact]
        public async Task GetByName_Should_ReturnCategory()
        {
            // Arrange
            var categoryResult = Category.Create("NameSearchTest", "Description for name search test");
            var category = categoryResult.Value;
            await _writeRepository.AddAsync(category, CancellationToken.None);
            await UnitOfWork.SaveChangesAsync(CancellationToken.None);

            // Act
            var foundByName = await _writeRepository.GetByNameAsync("NameSearchTest", CancellationToken.None);

            // Assert
            foundByName.ShouldNotBeNull();
            foundByName!.Name.ShouldBe("NameSearchTest");
            foundByName.Description.ShouldBe("Description for name search test");
        }
    }
}

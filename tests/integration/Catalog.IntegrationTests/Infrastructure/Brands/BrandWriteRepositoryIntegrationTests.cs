#nullable enable
using Catalog.Domain.Entities.BrandAggregate;
using Catalog.Domain.Entities.BrandAggregate.Specifications;
using Catalog.Infrastructure.Persistence;
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
    public class BrandWriteRepositoryIntegrationTests : BaseWriteRepoTestFixture<ApplicationWriteDbContext, IUnitOfWork>
    {
        private BrandWriteRepository _repository = null!;

        public BrandWriteRepositoryIntegrationTests(SharedTestcontainersFixture sharedFixture)
            : base(sharedFixture) { }

        protected override ApplicationWriteDbContext CreateWriteDbContext(DbContextOptions<ApplicationWriteDbContext> options)
        {
            // Provide null for ITenantInfo and use the default DatabaseStrategy if not needed for tests
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
            _repository = new BrandWriteRepository(WriteDbContext, httpContextAccessor);
        }

        [Fact]
        public async Task AddAndFindById_Should_WorkCorrectly()
        {
            // Arrange
            var brandResult = Brand.Create("TestBrand", "Test Description", "https://test.com");
            var brand = brandResult.Value;

            // Act
            await _repository.AddAsync(brand, CancellationToken.None);
            await UnitOfWork.SaveChangesAsync(CancellationToken.None);

            var brandByIdSpec = new BrandByIdSpecification(brand.Id);
            var retrievedBrand = await _repository.FirstOrDefaultAsync(brandByIdSpec, CancellationToken.None);

            // Assert
            retrievedBrand.ShouldNotBeNull();
            retrievedBrand!.Name.ShouldBe("TestBrand");
            retrievedBrand.Description.ShouldBe("Test Description");
            retrievedBrand.Website!.Value.ShouldBe("https://test.com");
        }

        [Fact]
        public async Task Update_Should_PersistChanges()
        {
            // Arrange
            var brandResult = Brand.Create("OriginalName", "Original description", "https://original.com");
            var brand = brandResult.Value;
            await _repository.AddAsync(brand, CancellationToken.None);
            await UnitOfWork.SaveChangesAsync(CancellationToken.None);

            // Act
            var updateResult = brand.Update("UpdatedName", "Updated description", "https://updated.com");
            updateResult.IsError.ShouldBeFalse();

            _repository.Update(brand);
            await UnitOfWork.SaveChangesAsync(CancellationToken.None);

            var brandByIdSpec = new BrandByIdSpecification(brand.Id);
            var updatedBrand = await _repository.FirstOrDefaultAsync(brandByIdSpec, CancellationToken.None);

            // Assert
            updatedBrand.ShouldNotBeNull();
            updatedBrand!.Name.ShouldBe("UpdatedName");
            updatedBrand.Description.ShouldBe("Updated description");
            updatedBrand.Website!.Value.ShouldBe("https://updated.com");
        }

        [Fact]
        public async Task Delete_Should_RemoveBrand()
        {
            // Arrange
            var brandResult = Brand.Create("BrandToDelete", "Will be deleted", "https://delete.com");
            var brand = brandResult.Value;
            await _repository.AddAsync(brand, CancellationToken.None);
            await UnitOfWork.SaveChangesAsync(CancellationToken.None);

            // Verify it exists
            var brandByIdSpec = new BrandByIdSpecification(brand.Id);
            var retrievedBrand = await _repository.FirstOrDefaultAsync(brandByIdSpec, CancellationToken.None);
            retrievedBrand.ShouldNotBeNull();

            // Act
            _repository.Delete(brand);
            await UnitOfWork.SaveChangesAsync(CancellationToken.None);

            // Assert
            var deletedBrand = await _repository.FirstOrDefaultAsync(brandByIdSpec, CancellationToken.None);
            deletedBrand.ShouldBeNull();
        }

        [Fact]
        public async Task FindBySpecification_Should_ReturnCorrectBrand()
        {
            // Arrange
            var brandResult = Brand.Create("SpecificBrand", "Find by specification", "https://specific.com");
            var brand = brandResult.Value;
            await _repository.AddAsync(brand, CancellationToken.None);
            await UnitOfWork.SaveChangesAsync(CancellationToken.None);

            // Act
            var foundBrand = await _repository.FirstOrDefaultAsync(
                new BrandByNameSpecification("SpecificBrand"),
                CancellationToken.None);

            // Assert
            foundBrand.ShouldNotBeNull();
            foundBrand!.Name.ShouldBe("SpecificBrand");
        }

        [Fact]
        public async Task ExistsWithName_Should_ReturnTrueForExistingName()
        {
            // Arrange
            var brandResult = Brand.Create("ExistingName", "Checking exists", "https://exists.com");
            var brand = brandResult.Value;
            await _repository.AddAsync(brand, CancellationToken.None);
            await UnitOfWork.SaveChangesAsync(CancellationToken.None);

            // Act
            var exists = await _repository.ExistsWithNameAsync("ExistingName", CancellationToken.None);

            // Assert
            exists.ShouldBeTrue();
        }

        [Fact]
        public async Task ExistsWithName_Should_ReturnFalseForNonExistentName()
        {
            // Act
            var exists = await _repository.ExistsWithNameAsync("NonExistentName", CancellationToken.None);

            // Assert
            exists.ShouldBeFalse();
        }

        [Fact]
        public async Task FindByName_Should_ReturnCorrectBrand()
        {
            // Arrange
            var brandResult = Brand.Create("NameToFind", "Find by name", "https://findbyname.com");
            var brand = brandResult.Value;
            await _repository.AddAsync(brand, CancellationToken.None);
            await UnitOfWork.SaveChangesAsync(CancellationToken.None);

            // Act
            var foundBrand = await _repository.FindByNameAsync("NameToFind", CancellationToken.None);

            // Assert
            foundBrand.ShouldNotBeNull();
            foundBrand!.Name.ShouldBe("NameToFind");
        }
    }
}

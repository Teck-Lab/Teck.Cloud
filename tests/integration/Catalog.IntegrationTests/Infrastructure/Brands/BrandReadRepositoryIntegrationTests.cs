#nullable enable
using System.Globalization;
using Ardalis.Specification;
using Catalog.Application.Brands.ReadModels;
using Catalog.Application.Brands.Repositories;
using Catalog.Application.Brands.Specifications;
using Catalog.Infrastructure.Persistence;
using Catalog.Infrastructure.Persistence.Repositories.Read;
using Catalog.IntegrationTests.Shared;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Catalog.IntegrationTests.Infrastructure.Brands
{
    [Collection("SharedTestcontainers")]
    public class BrandReadRepositoryIntegrationTests : BaseReadRepoTestFixture<ApplicationReadDbContext>
    {
        private IBrandReadRepository _readRepository = null!;

        public BrandReadRepositoryIntegrationTests(SharedTestcontainersFixture sharedFixture)
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
            _readRepository = new BrandReadRepository(ReadDbContext);
        }

        public override async ValueTask DisposeAsync()
        {
            await base.DisposeAsync();
            GC.SuppressFinalize(this);
        }

        [Fact]
        public async Task GetByIdAsync_Should_ReturnCorrectBrandReadModel()
        {
            // Arrange - seed a brand read-model directly into the read DB
            var r = new BrandReadModel { Id = Guid.NewGuid(), Name = "TestBrand", Description = "Test Description", Website = "https://test.com" };
            ReadDbContext.Set<BrandReadModel>().Add(r);
            await ReadDbContext.SaveChangesAsync(CancellationToken.None);

            // Act - Fetch using read repository
            var brandReadModel = await _readRepository.GetByIdAsync(r.Id, CancellationToken.None);

            // Assert
            brandReadModel.ShouldNotBeNull();
            brandReadModel!.Id.ShouldBe(r.Id);
            brandReadModel.Name.ShouldBe("TestBrand");
            brandReadModel.Description.ShouldBe("Test Description");
            brandReadModel.Website.ShouldBe("https://test.com");
        }

        [Fact]
        public async Task ListAsync_Should_ReturnAllBrands()
        {
            // Arrange - seed multiple brand read-models
            var rlist = new List<BrandReadModel>
            {
                new BrandReadModel { Id = Guid.NewGuid(), Name = "Brand1", Description = "Description 1", Website = "https://brand1.com" },
                new BrandReadModel { Id = Guid.NewGuid(), Name = "Brand2", Description = "Description 2", Website = "https://brand2.com" },
                new BrandReadModel { Id = Guid.NewGuid(), Name = "Brand3", Description = "Description 3", Website = "https://brand3.com" }
            };
            ReadDbContext.Set<BrandReadModel>().AddRange(rlist);
            await ReadDbContext.SaveChangesAsync(CancellationToken.None);

            // Act
            var spec = BrandReadSpecifications.GetAll();
            var result = await _readRepository.ListAsync(spec, CancellationToken.None);

            // Assert
            result.ShouldNotBeNull();
            result.Count.ShouldBeGreaterThanOrEqualTo(3);
            result.ShouldContain(b => b.Name == "Brand1");
            result.ShouldContain(b => b.Name == "Brand2");
            result.ShouldContain(b => b.Name == "Brand3");
        }

        [Fact]
        public async Task CountAsync_Should_ReturnCorrectCount()
        {
            // Arrange - seed multiple brand read-models
            var initialCount = await _readRepository.CountAsync(BrandReadSpecifications.GetAll(), CancellationToken.None);

            var rbrands = new List<BrandReadModel>
            {
                new BrandReadModel { Id = Guid.NewGuid(), Name = "CountBrand1", Description = "Count 1", Website = "https://count1.com" },
                new BrandReadModel { Id = Guid.NewGuid(), Name = "CountBrand2", Description = "Count 2", Website = "https://count2.com" }
            };
            ReadDbContext.Set<BrandReadModel>().AddRange(rbrands);
            await ReadDbContext.SaveChangesAsync(CancellationToken.None);

            // Act
            var count = await _readRepository.CountAsync(BrandReadSpecifications.GetAll(), CancellationToken.None);

            // Assert
            count.ShouldBe(initialCount + 2);
        }

        [Fact]
        public async Task FirstOrDefaultAsync_Should_ReturnBrandBySpecification()
        {
            // Arrange
            var rf = new BrandReadModel { Id = Guid.NewGuid(), Name = "FindMeBrand", Description = "Find this brand", Website = "https://findme.com" };
            ReadDbContext.Set<BrandReadModel>().Add(rf);
            await ReadDbContext.SaveChangesAsync(CancellationToken.None);

            // Act
            var result = await _readRepository.FirstOrDefaultAsync(
                BrandReadSpecifications.ByName("FindMeBrand"),
                CancellationToken.None);

            // Assert
            result.ShouldNotBeNull();
            result!.Name.ShouldBe("FindMeBrand");
        }

        [Fact]
        public async Task AnyAsync_Should_ReturnTrueWhenBrandExists()
        {
            // Arrange
            var re = new BrandReadModel { Id = Guid.NewGuid(), Name = "ExistingBrand", Description = "This brand exists", Website = "https://exists.com" };
            ReadDbContext.Set<BrandReadModel>().Add(re);
            await ReadDbContext.SaveChangesAsync(CancellationToken.None);

            // Act
            var exists = await _readRepository.AnyAsync(
                BrandReadSpecifications.ById(re.Id),
                CancellationToken.None);

            // Assert
            exists.ShouldBeTrue();
        }

        [Fact]
        public async Task AnyAsync_Should_ReturnFalseWhenBrandDoesNotExist()
        {
            // Act
            var exists = await _readRepository.AnyAsync(
                BrandReadSpecifications.ById(Guid.NewGuid()),
                CancellationToken.None);

            // Assert
            exists.ShouldBeFalse();
        }

        [Fact]
        public async Task ListAsync_WithPaginationSpecification_Should_ReturnPagedResults()
        {
            // Arrange - Create many brands to ensure pagination works
            var brandNames = Enumerable.Range(1, 20)
                    .Select(i => $"UniquePagedBrand{i:D2}")
                .ToList();

            var brands = brandNames
                .Select(name => new BrandReadModel { Id = Guid.NewGuid(), Name = name, Description = $"Description for {name}", Website = $"https://{name.ToLower(CultureInfo.InvariantCulture)}.com" })
                .ToList();

            ReadDbContext.Set<BrandReadModel>().AddRange(brands);
            await ReadDbContext.SaveChangesAsync(CancellationToken.None);

            // Act - Get page 2 with page size 5, filter by a unique prefix so only our inserted rows are considered
            var keyword = "UniquePagedBrand";
            var spec = new BrandPaginationSpecification(2, 5, keyword);
            var result = await _readRepository.ListAsync(spec, CancellationToken.None);

            // Assert - make deterministic by computing expected names from the inserted set
            result.ShouldNotBeNull();
            result.Count.ShouldBe(5); // Should return exactly 5 items

            var expectedNames = brandNames
                .Where(n => n.StartsWith("UniquePagedBrand", StringComparison.Ordinal))
                .OrderBy(n => n, StringComparer.Ordinal)
                .Skip(5) // page 2, page size 5 -> skip first 5
                .Take(5)
                .ToList();

            var actualNames = result.Select(rm => rm.Name).ToList();
            actualNames.ShouldBe(expectedNames);
        }
    }

    internal static class BrandReadSpecifications
    {
        public static Specification<BrandReadModel> GetAll() => new AllBrandsSpecification();
        public static Specification<BrandReadModel> ByName(string name) => new BrandByNameReadSpecification(name);
        public static Specification<BrandReadModel> ById(Guid id) => new BrandByIdReadSpecification(id);

        private class AllBrandsSpecification : Specification<BrandReadModel>
        {
            public AllBrandsSpecification()
            {
                Query.OrderBy(b => b.Name);
            }
        }

        private class BrandByNameReadSpecification : Specification<BrandReadModel>
        {
            public BrandByNameReadSpecification(string name)
            {
                Query.Where(b => b.Name == name);
            }
        }

        private class BrandByIdReadSpecification : Specification<BrandReadModel>
        {
            public BrandByIdReadSpecification(Guid id)
            {
                Query.Where(b => b.Id == id);
            }
        }
    }
}

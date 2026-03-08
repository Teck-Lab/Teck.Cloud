using Catalog.Application.Brands.ReadModels;
using Catalog.Infrastructure.Persistence;
using Catalog.Infrastructure.Persistence.Repositories.Read;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Catalog.UnitTests.Infrastructure.Persistence.Repositories.Read;

public sealed class BrandReadRepositoryTests : IDisposable
{
    private readonly ApplicationReadDbContext _dbContext;
    private readonly BrandReadRepository _repository;

    public BrandReadRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationReadDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new ApplicationReadDbContext(options, Catalog.UnitTests.Infrastructure.Persistence.TestTenantContextAccessor.Create());
        _repository = new BrandReadRepository(_dbContext);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnBrand_WhenExists()
    {
        // Arrange
        var brand = new BrandReadModel
        {
            Id = Guid.NewGuid(),
            Name = "Contoso",
            Description = "Contoso brand",
        };

        await _dbContext.Brands.AddAsync(brand, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.GetByIdAsync(brand.Id, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(brand.Id);
        result.Name.ShouldBe("Contoso");
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnNull_WhenNotFound()
    {
        // Act
        var result = await _repository.GetByIdAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeNull();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}

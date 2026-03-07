using Catalog.Domain.Entities.BrandAggregate;
using Catalog.Infrastructure.Persistence;
using Catalog.Infrastructure.Persistence.Repositories.Write;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;

namespace Catalog.UnitTests.Infrastructure.Persistence.Repositories.Write;

public sealed class BrandWriteRepositoryTests : IDisposable
{
    private readonly ApplicationWriteDbContext dbContext;
    private readonly BrandWriteRepository repository;

    public BrandWriteRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationWriteDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        this.dbContext = new ApplicationWriteDbContext(options, Catalog.UnitTests.Infrastructure.Persistence.TestTenantContextAccessor.Create());
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        this.repository = new BrandWriteRepository(this.dbContext, httpContextAccessor);
    }

    [Fact]
    public async Task ExistsWithNameAsync_ShouldReturnTrue_WhenBrandExists()
    {
        // Arrange
        var brand = Brand.Create("Contoso", "A brand", "https://contoso.com").Value;
        await this.dbContext.Brands.AddAsync(brand, TestContext.Current.CancellationToken);
        await this.dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await this.repository.ExistsWithNameAsync("Contoso", TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public async Task ExistsWithNameAsync_ShouldReturnFalse_WhenBrandDoesNotExist()
    {
        // Act
        var result = await this.repository.ExistsWithNameAsync("Missing", TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public async Task FindByNameAsync_ShouldReturnBrand_WhenBrandExists()
    {
        // Arrange
        var brand = Brand.Create("Fabrikam", "Another brand", "https://fabrikam.com").Value;
        await this.dbContext.Brands.AddAsync(brand, TestContext.Current.CancellationToken);
        await this.dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await this.repository.FindByNameAsync("Fabrikam", TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.Name.ShouldBe("Fabrikam");
    }

    [Fact]
    public async Task FindByNameAsync_ShouldReturnNull_WhenBrandDoesNotExist()
    {
        // Act
        var result = await this.repository.FindByNameAsync("Unknown", TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeNull();
    }

    public void Dispose()
    {
        this.dbContext.Dispose();
    }
}

using Catalog.Domain.Entities.CategoryAggregate;
using Catalog.Domain.Entities.ProductAggregate;
using Catalog.Infrastructure.Persistence;
using Catalog.Infrastructure.Persistence.Repositories.Write;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;

namespace Catalog.UnitTests.Infrastructure.Persistence.Repositories.Write;

public sealed class ProductWriteRepositoryTests : IDisposable
{
    private readonly ApplicationWriteDbContext _dbContext;
    private readonly ProductWriteRepository _repository;

    public ProductWriteRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationWriteDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new ApplicationWriteDbContext(options, Catalog.UnitTests.Infrastructure.Persistence.TestTenantContextAccessor.Create());
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        _repository = new ProductWriteRepository(_dbContext, httpContextAccessor);
    }

    [Fact]
    public async Task GetBySkuAsync_ShouldReturnProduct_WhenExists()
    {
        // Arrange
        var productResult = Product.Create(
            "Gaming Mouse",
            "RGB gaming mouse",
            "SKU-100",
            null,
            new List<Category>(),
            true);
        productResult.IsError.ShouldBeFalse();

        await _dbContext.Products.AddAsync(productResult.Value, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.GetBySkuAsync("SKU-100", TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.Name.ShouldBe("Gaming Mouse");
        result.ProductSKU.ShouldBe("SKU-100");
    }

    [Fact]
    public async Task GetBySkuAsync_ShouldReturnNull_WhenNotFound()
    {
        // Act
        var result = await _repository.GetBySkuAsync("SKU-MISSING", TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeNull();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}

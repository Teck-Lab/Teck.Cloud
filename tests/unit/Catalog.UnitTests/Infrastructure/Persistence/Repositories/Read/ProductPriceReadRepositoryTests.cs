using Catalog.Application.Products.ReadModels;
using Catalog.Infrastructure.Persistence;
using Catalog.Infrastructure.Persistence.Repositories.Read;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Catalog.UnitTests.Infrastructure.Persistence.Repositories.Read;

public sealed class ProductPriceReadRepositoryTests : IDisposable
{
    private readonly ApplicationReadDbContext _dbContext;
    private readonly ProductPriceReadRepository _repository;

    public ProductPriceReadRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationReadDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new ApplicationReadDbContext(options);
        _repository = new ProductPriceReadRepository(_dbContext);
    }

    [Fact]
    public async Task Constructor_ShouldInitializeRepository()
    {
        // Assert
        _repository.ShouldNotBeNull();
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnAllProductPrices()
    {
        // Arrange
        var productPrices = new[]
        {
            new ProductPriceReadModel
            {
                Id = Guid.NewGuid(),
                ProductId = Guid.NewGuid(),
                SalePrice = 99.99m,
                CurrencyCode = "USD",
                ProductPriceTypeId = Guid.NewGuid(),
                ProductPriceTypeName = "Retail"
            },
            new ProductPriceReadModel
            {
                Id = Guid.NewGuid(),
                ProductId = Guid.NewGuid(),
                SalePrice = 79.99m,
                CurrencyCode = "USD",
                ProductPriceTypeId = Guid.NewGuid(),
                ProductPriceTypeName = "Wholesale"
            }
        };
        await _dbContext.ProductPrices.AddRangeAsync(productPrices, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.GetAllAsync(enableTracking: false, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.Count.ShouldBe(2);
        result.ShouldContain(p => p.SalePrice == 99.99m);
        result.ShouldContain(p => p.SalePrice == 79.99m);
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnEmptyList_WhenNoPrices()
    {
        // Act
        var result = await _repository.GetAllAsync(enableTracking: false, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task FindByIdAsync_ShouldReturnProductPrice_WhenExists()
    {
        // Arrange
        var productPrice = new ProductPriceReadModel
        {
            Id = Guid.NewGuid(),
            ProductId = Guid.NewGuid(),
            SalePrice = 149.99m,
            CurrencyCode = "EUR",
            ProductPriceTypeId = Guid.NewGuid(),
            ProductPriceTypeName = "Member"
        };
        await _dbContext.ProductPrices.AddAsync(productPrice, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.FindByIdAsync(productPrice.Id, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(productPrice.Id);
        result.ProductId.ShouldBe(productPrice.ProductId);
        result.SalePrice.ShouldBe(149.99m);
        result.CurrencyCode.ShouldBe("EUR");
        result.ProductPriceTypeName.ShouldBe("Member");
    }

    [Fact]
    public async Task FindByIdAsync_ShouldReturnNull_WhenNotExists()
    {
        // Act
        var result = await _repository.FindByIdAsync(Guid.NewGuid(), cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task FindAsync_ShouldReturnMatchingProductPrices()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var productPrices = new[]
        {
            new ProductPriceReadModel
            {
                Id = Guid.NewGuid(),
                ProductId = productId,
                SalePrice = 99.99m,
                CurrencyCode = "USD"
            },
            new ProductPriceReadModel
            {
                Id = Guid.NewGuid(),
                ProductId = productId,
                SalePrice = 79.99m,
                CurrencyCode = "USD"
            },
            new ProductPriceReadModel
            {
                Id = Guid.NewGuid(),
                ProductId = Guid.NewGuid(),
                SalePrice = 59.99m,
                CurrencyCode = "USD"
            }
        };
        await _dbContext.ProductPrices.AddRangeAsync(productPrices, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.FindAsync(
            predicate: p => p.ProductId == productId,
            enableTracking: false,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.Count.ShouldBe(2);
        result.ShouldAllBe(p => p.ProductId == productId);
    }

    [Fact]
    public async Task ExistsAsync_ShouldReturnTrue_WhenMatchExists()
    {
        // Arrange
        var productPrices = new[]
        {
            new ProductPriceReadModel { Id = Guid.NewGuid(), ProductId = Guid.NewGuid(), SalePrice = 100m, CurrencyCode = "USD" },
            new ProductPriceReadModel { Id = Guid.NewGuid(), ProductId = Guid.NewGuid(), SalePrice = 200m, CurrencyCode = "EUR" }
        };
        await _dbContext.ProductPrices.AddRangeAsync(productPrices, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.ExistsAsync(
            predicate: p => p.CurrencyCode == "USD",
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public async Task ExistsAsync_ShouldReturnFalse_WhenNoMatchExists()
    {
        // Arrange
        var productPrice = new ProductPriceReadModel
        {
            Id = Guid.NewGuid(),
            ProductId = Guid.NewGuid(),
            SalePrice = 50m,
            CurrencyCode = "USD"
        };
        await _dbContext.ProductPrices.AddAsync(productPrice, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.ExistsAsync(
            predicate: p => p.CurrencyCode == "GBP",
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public async Task FindOneAsync_ShouldReturnFirstMatch_WhenMultipleExist()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var productPrices = new[]
        {
            new ProductPriceReadModel
            {
                Id = Guid.NewGuid(),
                ProductId = productId,
                SalePrice = 99.99m,
                CurrencyCode = "USD",
                ProductPriceTypeName = "Retail"
            },
            new ProductPriceReadModel
            {
                Id = Guid.NewGuid(),
                ProductId = productId,
                SalePrice = 79.99m,
                CurrencyCode = "USD",
                ProductPriceTypeName = "Wholesale"
            }
        };
        await _dbContext.ProductPrices.AddRangeAsync(productPrices, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.FindOneAsync(
            predicate: p => p.ProductId == productId,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.ProductId.ShouldBe(productId);
    }

    [Fact]
    public async Task FindOneAsync_ShouldReturnNull_WhenNoMatch()
    {
        // Act
        var result = await _repository.FindOneAsync(
            predicate: p => p.SalePrice > 1000m,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task FindByIdsAsync_ShouldReturnMatchingPrices()
    {
        // Arrange
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var id3 = Guid.NewGuid();
        var productPrices = new[]
        {
            new ProductPriceReadModel { Id = id1, ProductId = Guid.NewGuid(), SalePrice = 10m },
            new ProductPriceReadModel { Id = id2, ProductId = Guid.NewGuid(), SalePrice = 20m },
            new ProductPriceReadModel { Id = id3, ProductId = Guid.NewGuid(), SalePrice = 30m }
        };
        await _dbContext.ProductPrices.AddRangeAsync(productPrices, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.FindByIdsAsync(new[] { id1, id3 }, TestContext.Current.CancellationToken);

        // Assert
        result.Count.ShouldBe(2);
        result.ShouldContain(p => p.Id == id1);
        result.ShouldContain(p => p.Id == id3);
    }

    public void Dispose()
    {
        _dbContext?.Dispose();
    }
}

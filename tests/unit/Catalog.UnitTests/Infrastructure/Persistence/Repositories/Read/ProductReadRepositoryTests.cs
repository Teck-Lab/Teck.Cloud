using Catalog.Application.Products.ReadModels;
using Catalog.Infrastructure.Persistence;
using Catalog.Infrastructure.Persistence.Repositories.Read;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Catalog.UnitTests.Infrastructure.Persistence.Repositories.Read;

public sealed class ProductReadRepositoryTests : IDisposable
{
    private readonly ApplicationReadDbContext _dbContext;
    private readonly ProductReadRepository _repository;

    public ProductReadRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationReadDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new ApplicationReadDbContext(options);
        _repository = new ProductReadRepository(_dbContext);
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnAllProducts()
    {
        // Arrange
        var products = new[]
        {
            new ProductReadModel { Id = Guid.NewGuid(), Name = "Product 1", Sku = "SKU001" },
            new ProductReadModel { Id = Guid.NewGuid(), Name = "Product 2", Sku = "SKU002" },
            new ProductReadModel { Id = Guid.NewGuid(), Name = "Product 3", Sku = "SKU003" }
        };
        await _dbContext.Products.AddRangeAsync(products, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.GetAllAsync(TestContext.Current.CancellationToken);

        // Assert
        result.Count.ShouldBe(3);
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnEmptyList_WhenNoProducts()
    {
        // Act
        var result = await _repository.GetAllAsync(TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnProduct_WhenExists()
    {
        // Arrange
        var product = new ProductReadModel { Id = Guid.NewGuid(), Name = "Test Product", Sku = "SKU001" };
        await _dbContext.Products.AddAsync(product, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.GetByIdAsync(product.Id, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(product.Id);
        result.Name.ShouldBe("Test Product");
        result.Sku.ShouldBe("SKU001");
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnNull_WhenNotExists()
    {
        // Act
        var result = await _repository.GetByIdAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetByBrandIdAsync_ShouldReturnProductsOfBrand()
    {
        // Arrange
        var brandId = Guid.NewGuid();
        var products = new[]
        {
            new ProductReadModel { Id = Guid.NewGuid(), Name = "Product 1", Sku = "SKU001", BrandId = brandId },
            new ProductReadModel { Id = Guid.NewGuid(), Name = "Product 2", Sku = "SKU002", BrandId = brandId },
            new ProductReadModel { Id = Guid.NewGuid(), Name = "Product 3", Sku = "SKU003", BrandId = Guid.NewGuid() }
        };
        await _dbContext.Products.AddRangeAsync(products, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.GetByBrandIdAsync(brandId, TestContext.Current.CancellationToken);

        // Assert
        result.Count.ShouldBe(2);
        result.ShouldAllBe(p => p.BrandId == brandId);
    }

    [Fact]
    public async Task GetByBrandIdAsync_ShouldReturnEmpty_WhenNoBrandProducts()
    {
        // Act
        var result = await _repository.GetByBrandIdAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetByCategoryIdAsync_ShouldReturnProductsOfCategory()
    {
        // Arrange
        var categoryId = Guid.NewGuid();
        var products = new[]
        {
            new ProductReadModel { Id = Guid.NewGuid(), Name = "Product 1", Sku = "SKU001", CategoryId = categoryId },
            new ProductReadModel { Id = Guid.NewGuid(), Name = "Product 2", Sku = "SKU002", CategoryId = categoryId },
            new ProductReadModel { Id = Guid.NewGuid(), Name = "Product 3", Sku = "SKU003", CategoryId = Guid.NewGuid() }
        };
        await _dbContext.Products.AddRangeAsync(products, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.GetByCategoryIdAsync(categoryId, TestContext.Current.CancellationToken);

        // Assert
        result.Count.ShouldBe(2);
        result.ShouldAllBe(p => p.CategoryId == categoryId);
    }

    [Fact]
    public async Task GetByCategoryIdAsync_ShouldReturnEmpty_WhenNoCategoryProducts()
    {
        // Act
        var result = await _repository.GetByCategoryIdAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetBySkuAsync_ShouldReturnProduct_WhenExists()
    {
        // Arrange
        var product = new ProductReadModel { Id = Guid.NewGuid(), Name = "Test Product", Sku = "SKU-UNIQUE-001" };
        await _dbContext.Products.AddAsync(product, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.GetBySkuAsync("SKU-UNIQUE-001", TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(product.Id);
        result.Sku.ShouldBe("SKU-UNIQUE-001");
    }

    [Fact]
    public async Task GetBySkuAsync_ShouldReturnNull_WhenNotExists()
    {
        // Act
        var result = await _repository.GetBySkuAsync("NON-EXISTENT-SKU", TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetPagedProductsAsync_ShouldReturnPagedResults()
    {
        // Arrange
        var products = Enumerable.Range(1, 15)
            .Select(i => new ProductReadModel { Id = Guid.NewGuid(), Name = $"Product {i}", Sku = $"SKU{i:D3}" })
            .ToList();
        await _dbContext.Products.AddRangeAsync(products, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.GetPagedProductsAsync(2, 5, null, TestContext.Current.CancellationToken);

        // Assert
        result.Items.Count.ShouldBe(5);
        result.TotalItems.ShouldBe(15);
        result.Page.ShouldBe(2);
        result.Size.ShouldBe(5);
        result.TotalPages.ShouldBe(3);
    }

    [Fact]
    public async Task GetPagedProductsAsync_ShouldFilterByKeyword_InName()
    {
        // Arrange
        var products = new[]
        {
            new ProductReadModel { Id = Guid.NewGuid(), Name = "Laptop Computer", Sku = "SKU001" },
            new ProductReadModel { Id = Guid.NewGuid(), Name = "Desktop Computer", Sku = "SKU002" },
            new ProductReadModel { Id = Guid.NewGuid(), Name = "Tablet Device", Sku = "SKU003" }
        };
        await _dbContext.Products.AddRangeAsync(products, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.GetPagedProductsAsync(1, 10, "Computer", TestContext.Current.CancellationToken);

        // Assert
        result.Items.Count.ShouldBe(2);
        result.TotalItems.ShouldBe(2);
    }

    [Fact]
    public async Task GetPagedProductsAsync_ShouldFilterByKeyword_InDescription()
    {
        // Arrange
        var products = new[]
        {
            new ProductReadModel { Id = Guid.NewGuid(), Name = "Prod1", Sku = "SKU001", Description = "High performance laptop" },
            new ProductReadModel { Id = Guid.NewGuid(), Name = "Prod2", Sku = "SKU002", Description = "Gaming desktop" },
            new ProductReadModel { Id = Guid.NewGuid(), Name = "Prod3", Sku = "SKU003", Description = "High performance server" }
        };
        await _dbContext.Products.AddRangeAsync(products, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.GetPagedProductsAsync(1, 10, "performance", TestContext.Current.CancellationToken);

        // Assert
        result.Items.Count.ShouldBe(2);
    }

    [Fact]
    public async Task GetPagedProductsAsync_ShouldReturnAllResults_WhenKeywordIsEmpty()
    {
        // Arrange
        var products = new[]
        {
            new ProductReadModel { Id = Guid.NewGuid(), Name = "Product 1", Sku = "SKU001" },
            new ProductReadModel { Id = Guid.NewGuid(), Name = "Product 2", Sku = "SKU002" }
        };
        await _dbContext.Products.AddRangeAsync(products, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.GetPagedProductsAsync(1, 10, "", TestContext.Current.CancellationToken);

        // Assert
        result.Items.Count.ShouldBe(2);
    }

    [Fact]
    public async Task GetPagedProductsAsync_ShouldReturnAllResults_WhenKeywordIsNull()
    {
        // Arrange
        var products = new[]
        {
            new ProductReadModel { Id = Guid.NewGuid(), Name = "Product 1", Sku = "SKU001" },
            new ProductReadModel { Id = Guid.NewGuid(), Name = "Product 2", Sku = "SKU002" },
            new ProductReadModel { Id = Guid.NewGuid(), Name = "Product 3", Sku = "SKU003" }
        };
        await _dbContext.Products.AddRangeAsync(products, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.GetPagedProductsAsync(1, 10, null, TestContext.Current.CancellationToken);

        // Assert
        result.Items.Count.ShouldBe(3);
    }

    [Fact]
    public async Task GetPagedProductsAsync_ShouldOrderByName()
    {
        // Arrange
        var products = new[]
        {
            new ProductReadModel { Id = Guid.NewGuid(), Name = "Zebra Product", Sku = "SKU001" },
            new ProductReadModel { Id = Guid.NewGuid(), Name = "Apple Product", Sku = "SKU002" },
            new ProductReadModel { Id = Guid.NewGuid(), Name = "Mango Product", Sku = "SKU003" }
        };
        await _dbContext.Products.AddRangeAsync(products, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.GetPagedProductsAsync(1, 10, null, TestContext.Current.CancellationToken);

        // Assert
        result.Items[0].Name.ShouldBe("Apple Product");
        result.Items[1].Name.ShouldBe("Mango Product");
        result.Items[2].Name.ShouldBe("Zebra Product");
    }

    public void Dispose()
    {
        _dbContext?.Dispose();
    }
}

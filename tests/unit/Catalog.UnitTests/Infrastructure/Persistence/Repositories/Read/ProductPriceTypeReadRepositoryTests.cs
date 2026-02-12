using Catalog.Application.ProductPriceTypes.ReadModels;
using Catalog.Infrastructure.Persistence;
using Catalog.Infrastructure.Persistence.Repositories.Read;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Catalog.UnitTests.Infrastructure.Persistence.Repositories.Read;

public sealed class ProductPriceTypeReadRepositoryTests : IDisposable
{
    private readonly ApplicationReadDbContext _dbContext;
    private readonly ProductPriceTypeReadRepository _repository;

    public ProductPriceTypeReadRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationReadDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new ApplicationReadDbContext(options);
        _repository = new ProductPriceTypeReadRepository(_dbContext);
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnAllProductPriceTypes()
    {
        // Arrange
        var priceTypes = new[]
        {
            new ProductPriceTypeReadModel { Id = Guid.NewGuid(), Name = "Retail", Description = "Standard retail price" },
            new ProductPriceTypeReadModel { Id = Guid.NewGuid(), Name = "Wholesale", Description = "Bulk purchase price" },
            new ProductPriceTypeReadModel { Id = Guid.NewGuid(), Name = "Member", Description = "Member discount price" }
        };
        await _dbContext.ProductPriceTypes.AddRangeAsync(priceTypes, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.GetAllAsync(TestContext.Current.CancellationToken);

        // Assert
        result.Count.ShouldBe(3);
        result.ShouldContain(p => p.Name == "Retail");
        result.ShouldContain(p => p.Name == "Wholesale");
        result.ShouldContain(p => p.Name == "Member");
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnEmptyList_WhenNoPriceTypes()
    {
        // Act
        var result = await _repository.GetAllAsync(TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnPriceType_WhenExists()
    {
        // Arrange
        var priceType = new ProductPriceTypeReadModel
        {
            Id = Guid.NewGuid(),
            Name = "Promotional",
            Description = "Special promotional pricing"
        };
        await _dbContext.ProductPriceTypes.AddAsync(priceType, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.GetByIdAsync(priceType.Id, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(priceType.Id);
        result.Name.ShouldBe("Promotional");
        result.Description.ShouldBe("Special promotional pricing");
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
    public async Task GetPagedProductPriceTypesAsync_ShouldReturnPagedResults()
    {
        // Arrange
        var priceTypes = Enumerable.Range(1, 25)
            .Select(i => new ProductPriceTypeReadModel
            {
                Id = Guid.NewGuid(),
                Name = $"Price Type {i:D2}",
                Description = $"Description {i}"
            })
            .ToArray();
        await _dbContext.ProductPriceTypes.AddRangeAsync(priceTypes, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.GetPagedProductPriceTypesAsync(2, 10, null, TestContext.Current.CancellationToken);

        // Assert
        result.Items.Count.ShouldBe(10);
        result.Page.ShouldBe(2);
        result.Size.ShouldBe(10);
        result.TotalItems.ShouldBe(25);
        result.TotalPages.ShouldBe(3);
    }

    [Fact]
    public async Task GetPagedProductPriceTypesAsync_ShouldFilterByKeyword_InName()
    {
        // Arrange
        var priceTypes = new[]
        {
            new ProductPriceTypeReadModel { Id = Guid.NewGuid(), Name = "Retail Standard", Description = "Normal retail price" },
            new ProductPriceTypeReadModel { Id = Guid.NewGuid(), Name = "Wholesale Bulk", Description = "Large orders" },
            new ProductPriceTypeReadModel { Id = Guid.NewGuid(), Name = "Retail Premium", Description = "Premium tier" }
        };
        await _dbContext.ProductPriceTypes.AddRangeAsync(priceTypes, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.GetPagedProductPriceTypesAsync(1, 10, "Retail", TestContext.Current.CancellationToken);

        // Assert
        result.Items.Count.ShouldBe(2);
        result.TotalItems.ShouldBe(2);
        result.Items.ShouldAllBe(p => p.Name.Contains("Retail"));
    }

    [Fact]
    public async Task GetPagedProductPriceTypesAsync_ShouldFilterByKeyword_InDescription()
    {
        // Arrange
        var priceTypes = new[]
        {
            new ProductPriceTypeReadModel { Id = Guid.NewGuid(), Name = "Type A", Description = "Premium service included" },
            new ProductPriceTypeReadModel { Id = Guid.NewGuid(), Name = "Type B", Description = "Standard service" },
            new ProductPriceTypeReadModel { Id = Guid.NewGuid(), Name = "Type C", Description = "Premium features" }
        };
        await _dbContext.ProductPriceTypes.AddRangeAsync(priceTypes, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.GetPagedProductPriceTypesAsync(1, 10, "Premium", TestContext.Current.CancellationToken);

        // Assert
        result.Items.Count.ShouldBe(2);
        result.TotalItems.ShouldBe(2);
        result.Items.ShouldAllBe(p => p.Description != null && p.Description.Contains("Premium"));
    }

    [Fact]
    public async Task GetPagedProductPriceTypesAsync_ShouldReturnAllResults_WhenKeywordIsNull()
    {
        // Arrange
        var priceTypes = new[]
        {
            new ProductPriceTypeReadModel { Id = Guid.NewGuid(), Name = "Type 1" },
            new ProductPriceTypeReadModel { Id = Guid.NewGuid(), Name = "Type 2" },
            new ProductPriceTypeReadModel { Id = Guid.NewGuid(), Name = "Type 3" }
        };
        await _dbContext.ProductPriceTypes.AddRangeAsync(priceTypes, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.GetPagedProductPriceTypesAsync(1, 10, null, TestContext.Current.CancellationToken);

        // Assert
        result.Items.Count.ShouldBe(3);
        result.TotalItems.ShouldBe(3);
    }

    [Fact]
    public async Task GetPagedProductPriceTypesAsync_ShouldReturnAllResults_WhenKeywordIsEmpty()
    {
        // Arrange
        var priceTypes = new[]
        {
            new ProductPriceTypeReadModel { Id = Guid.NewGuid(), Name = "Type 1" },
            new ProductPriceTypeReadModel { Id = Guid.NewGuid(), Name = "Type 2" }
        };
        await _dbContext.ProductPriceTypes.AddRangeAsync(priceTypes, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.GetPagedProductPriceTypesAsync(1, 10, string.Empty, TestContext.Current.CancellationToken);

        // Assert
        result.Items.Count.ShouldBe(2);
        result.TotalItems.ShouldBe(2);
    }

    [Fact]
    public async Task GetPagedProductPriceTypesAsync_ShouldOrderByName()
    {
        // Arrange
        var priceTypes = new[]
        {
            new ProductPriceTypeReadModel { Id = Guid.NewGuid(), Name = "Zebra Pricing" },
            new ProductPriceTypeReadModel { Id = Guid.NewGuid(), Name = "Alpha Pricing" },
            new ProductPriceTypeReadModel { Id = Guid.NewGuid(), Name = "Beta Pricing" }
        };
        await _dbContext.ProductPriceTypes.AddRangeAsync(priceTypes, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.GetPagedProductPriceTypesAsync(1, 10, null, TestContext.Current.CancellationToken);

        // Assert
        result.Items.Count.ShouldBe(3);
        result.Items[0].Name.ShouldBe("Alpha Pricing");
        result.Items[1].Name.ShouldBe("Beta Pricing");
        result.Items[2].Name.ShouldBe("Zebra Pricing");
    }

    public void Dispose()
    {
        _dbContext?.Dispose();
    }
}

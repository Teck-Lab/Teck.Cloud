using Catalog.Domain.Entities.ProductPriceTypeAggregate;
using Catalog.Infrastructure.Persistence;
using Catalog.Infrastructure.Persistence.Repositories.Write;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;

namespace Catalog.UnitTests.Infrastructure.Persistence.Repositories.Write;

public sealed class ProductPriceTypeWriteRepositoryTests : IDisposable
{
    private readonly ApplicationWriteDbContext _dbContext;
    private readonly ProductPriceTypeWriteRepository _repository;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ProductPriceTypeWriteRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationWriteDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new ApplicationWriteDbContext(options);
        _httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        _repository = new ProductPriceTypeWriteRepository(_dbContext, _httpContextAccessor);
    }

    [Fact]
    public async Task GetByNameAsync_ShouldReturnProductPriceType_WhenExists()
    {
        // Arrange
        var priceType = ProductPriceType.Create("Retail", 1).Value;
        await _dbContext.ProductPriceTypes.AddAsync(priceType, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.GetByNameAsync("Retail", TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.Name.ShouldBe("Retail");
        result.Priority.ShouldBe(1);
    }

    [Fact]
    public async Task GetByNameAsync_ShouldReturnNull_WhenNotExists()
    {
        // Act
        var result = await _repository.GetByNameAsync("NonExistent", TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetByNameAsync_ShouldBeCaseSensitive()
    {
        // Arrange
        var priceType = ProductPriceType.Create("Wholesale", 2).Value;
        await _dbContext.ProductPriceTypes.AddAsync(priceType, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.GetByNameAsync("wholesale", TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task AddAsync_ShouldAddProductPriceType()
    {
        // Arrange
        var priceType = ProductPriceType.Create("Member", 3).Value;

        // Act
        await _repository.AddAsync(priceType, TestContext.Current.CancellationToken);
        await _repository.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        var result = await _dbContext.ProductPriceTypes.FirstOrDefaultAsync(
            p => p.Name == "Member", 
            TestContext.Current.CancellationToken);
        result.ShouldNotBeNull();
        result.Priority.ShouldBe(3);
    }

    [Fact]
    public async Task Update_ShouldUpdateProductPriceType()
    {
        // Arrange
        var priceType = ProductPriceType.Create("Standard", 1).Value;
        await _dbContext.ProductPriceTypes.AddAsync(priceType, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        priceType.Update("Premium", 5);
        _repository.Update(priceType);
        await _repository.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        var result = await _dbContext.ProductPriceTypes.FindAsync(new object[] { priceType.Id }, TestContext.Current.CancellationToken);
        result.ShouldNotBeNull();
        result.Name.ShouldBe("Premium");
        result.Priority.ShouldBe(5);
    }

    [Fact]
    public async Task Delete_ShouldRemoveProductPriceType()
    {
        // Arrange
        var priceType = ProductPriceType.Create("ToDelete", 1).Value;
        await _dbContext.ProductPriceTypes.AddAsync(priceType, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        _repository.Delete(priceType);
        await _repository.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        var result = await _dbContext.ProductPriceTypes.FindAsync(new object[] { priceType.Id }, TestContext.Current.CancellationToken);
        result.ShouldBeNull();
    }

    [Fact]
    public async Task DeleteRange_ShouldRemoveMultipleProductPriceTypes()
    {
        // Arrange
        var priceType1 = ProductPriceType.Create("Type1", 1).Value;
        var priceType2 = ProductPriceType.Create("Type2", 2).Value;
        await _dbContext.ProductPriceTypes.AddRangeAsync(new[] { priceType1, priceType2 }, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        _repository.DeleteRange(new[] { priceType1, priceType2 });
        await _repository.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        var count = await _dbContext.ProductPriceTypes.CountAsync(TestContext.Current.CancellationToken);
        count.ShouldBe(0);
    }

    [Fact]
    public async Task FindByIdAsync_ShouldReturnProductPriceType_WhenExists()
    {
        // Arrange
        var priceType = ProductPriceType.Create("VIP", 10).Value;
        await _dbContext.ProductPriceTypes.AddAsync(priceType, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.FindByIdAsync(priceType.Id, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(priceType.Id);
        result.Name.ShouldBe("VIP");
        result.Priority.ShouldBe(10);
    }

    [Fact]
    public async Task FindByIdAsync_ShouldReturnNull_WhenNotExists()
    {
        // Act
        var result = await _repository.FindByIdAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task FindByIdsAsync_ShouldReturnMatchingProductPriceTypes()
    {
        // Arrange
        var priceType1 = ProductPriceType.Create("Type1", 1).Value;
        var priceType2 = ProductPriceType.Create("Type2", 2).Value;
        var priceType3 = ProductPriceType.Create("Type3", 3).Value;
        await _dbContext.ProductPriceTypes.AddRangeAsync(new[] { priceType1, priceType2, priceType3 }, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.FindByIdsAsync(new[] { priceType1.Id, priceType3.Id }, TestContext.Current.CancellationToken);

        // Assert
        result.Count.ShouldBe(2);
        result.ShouldContain(p => p.Id == priceType1.Id);
        result.ShouldContain(p => p.Id == priceType3.Id);
    }

    [Fact]
    public async Task FindOneAsync_ShouldReturnProductPriceType_WhenMatches()
    {
        // Arrange
        var priceType = ProductPriceType.Create("UniqueType", 99).Value;
        await _dbContext.ProductPriceTypes.AddAsync(priceType, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.FindOneAsync(p => p.Name == "UniqueType", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.Name.ShouldBe("UniqueType");
        result.Priority.ShouldBe(99);
    }

    [Fact]
    public async Task FindAsync_ShouldReturnMatchingProductPriceTypes()
    {
        // Arrange
        var priceType1 = ProductPriceType.Create("Retail", 1).Value;
        var priceType2 = ProductPriceType.Create("Wholesale", 2).Value;
        var priceType3 = ProductPriceType.Create("Member", 3).Value;
        await _dbContext.ProductPriceTypes.AddRangeAsync(new[] { priceType1, priceType2, priceType3 }, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.FindAsync(p => p.Priority > 1, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.Count.ShouldBe(2);
        result.ShouldAllBe(p => p.Priority > 1);
        result.ShouldContain(p => p.Name == "Wholesale");
        result.ShouldContain(p => p.Name == "Member");
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnAllProductPriceTypes()
    {
        // Arrange
        var priceType1 = ProductPriceType.Create("Type1", 1).Value;
        var priceType2 = ProductPriceType.Create("Type2", 2).Value;
        await _dbContext.ProductPriceTypes.AddRangeAsync(new[] { priceType1, priceType2 }, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.GetAllAsync(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.Count.ShouldBe(2);
    }

    [Fact]
    public async Task ExistsAsync_ShouldReturnTrue_WhenExists()
    {
        // Arrange
        var priceType = ProductPriceType.Create("ExistsType", 5).Value;
        await _dbContext.ProductPriceTypes.AddAsync(priceType, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.ExistsAsync(p => p.Name == "ExistsType", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public async Task ExistsAsync_ShouldReturnFalse_WhenNotExists()
    {
        // Act
        var result = await _repository.ExistsAsync(p => p.Name == "DoesNotExist", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public async Task ExistsAsync_ShouldReturnTrue_WhenPriorityMatches()
    {
        // Arrange
        var priceType = ProductPriceType.Create("HighPriority", 100).Value;
        await _dbContext.ProductPriceTypes.AddAsync(priceType, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.ExistsAsync(p => p.Priority >= 50, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeTrue();
    }

    public void Dispose()
    {
        _dbContext?.Dispose();
    }
}

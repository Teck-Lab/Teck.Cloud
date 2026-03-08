using Catalog.Domain.Entities.CategoryAggregate;
using Catalog.Domain.Entities.ProductAggregate;
using Catalog.Domain.Entities.PromotionAggregate;
using Catalog.Infrastructure.Persistence;
using Catalog.Infrastructure.Persistence.Repositories.Write;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;

namespace Catalog.UnitTests.Infrastructure.Persistence.Repositories.Write;

public sealed class PromotionWriteRepositoryTests : IDisposable
{
    private readonly ApplicationWriteDbContext _dbContext;
    private readonly PromotionWriteRepository _repository;

    public PromotionWriteRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationWriteDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new ApplicationWriteDbContext(options, Catalog.UnitTests.Infrastructure.Persistence.TestTenantContextAccessor.Create());
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        _repository = new PromotionWriteRepository(_dbContext, httpContextAccessor);
    }

    [Fact]
    public async Task GetByNameAsync_ShouldReturnPromotion_WhenExists()
    {
        // Arrange
        var promotion = CreatePromotion("Spring Sale", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(2));
        await _dbContext.Promotions.AddAsync(promotion, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.GetByNameAsync("Spring Sale", TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.Name.ShouldBe("Spring Sale");
    }

    [Fact]
    public async Task GetByNameAsync_ShouldReturnNull_WhenNotFound()
    {
        // Act
        var result = await _repository.GetByNameAsync("Missing Promotion", TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetActivePromotionsAsync_ShouldReturnOnlyCurrentlyActivePromotions()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var activePromotion = CreatePromotion("Active", now.AddDays(-2), now.AddDays(2));
        var expiredPromotion = CreatePromotion("Expired", now.AddDays(-10), now.AddDays(-5));

        await _dbContext.Promotions.AddRangeAsync([activePromotion, expiredPromotion], TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.GetActivePromotionsAsync(TestContext.Current.CancellationToken);

        // Assert
        result.Count.ShouldBe(1);
        result[0].Name.ShouldBe("Active");
    }

    private static Promotion CreatePromotion(string name, DateTimeOffset validFrom, DateTimeOffset validTo)
    {
        var productResult = Product.Create(
            $"{name} Product",
            "Product used in promotion",
            $"SKU-{name.ToUpperInvariant()}",
            null,
            new List<Category>(),
            true);
        productResult.IsError.ShouldBeFalse();

        var promotionResult = Promotion.Create(
            name,
            $"{name} description",
            validFrom,
            validTo,
            new List<Product> { productResult.Value });
        promotionResult.IsError.ShouldBeFalse();

        return promotionResult.Value;
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}

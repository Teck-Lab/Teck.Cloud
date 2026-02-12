using Catalog.Application.Promotions.ReadModels;
using Catalog.Infrastructure.Persistence;
using Catalog.Infrastructure.Persistence.Repositories.Read;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Catalog.UnitTests.Infrastructure.Persistence.Repositories.Read;

public sealed class PromotionReadRepositoryTests : IDisposable
{
    private readonly ApplicationReadDbContext _dbContext;
    private readonly PromotionReadRepository _repository;

    public PromotionReadRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationReadDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new ApplicationReadDbContext(options);
        _repository = new PromotionReadRepository(_dbContext);
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnAllPromotions()
    {
        // Arrange
        var promotions = new[]
        {
            new PromotionReadModel { Id = Guid.NewGuid(), Name = "Promo 1", StartDate = DateTimeOffset.UtcNow, EndDate = DateTimeOffset.UtcNow.AddDays(7) },
            new PromotionReadModel { Id = Guid.NewGuid(), Name = "Promo 2", StartDate = DateTimeOffset.UtcNow, EndDate = DateTimeOffset.UtcNow.AddDays(14) },
            new PromotionReadModel { Id = Guid.NewGuid(), Name = "Promo 3", StartDate = DateTimeOffset.UtcNow, EndDate = DateTimeOffset.UtcNow.AddDays(21) }
        };
        await _dbContext.Promotions.AddRangeAsync(promotions, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.GetAllAsync(TestContext.Current.CancellationToken);

        // Assert
        result.Count.ShouldBe(3);
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnEmptyList_WhenNoPromotions()
    {
        // Act
        var result = await _repository.GetAllAsync(TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnPromotion_WhenExists()
    {
        // Arrange
        var promotion = new PromotionReadModel 
        { 
            Id = Guid.NewGuid(), 
            Name = "Test Promotion",
            StartDate = DateTimeOffset.UtcNow,
            EndDate = DateTimeOffset.UtcNow.AddDays(7)
        };
        await _dbContext.Promotions.AddAsync(promotion, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.GetByIdAsync(promotion.Id, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(promotion.Id);
        result.Name.ShouldBe("Test Promotion");
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
    public async Task GetActivePromotionsAsync_ShouldReturnOnlyActivePromotions()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var promotions = new[]
        {
            new PromotionReadModel { Id = Guid.NewGuid(), Name = "Active 1", StartDate = now.AddDays(-1), EndDate = now.AddDays(7) },
            new PromotionReadModel { Id = Guid.NewGuid(), Name = "Active 2", StartDate = now.AddDays(-2), EndDate = now.AddDays(5) },
            new PromotionReadModel { Id = Guid.NewGuid(), Name = "Past", StartDate = now.AddDays(-30), EndDate = now.AddDays(-1) },
            new PromotionReadModel { Id = Guid.NewGuid(), Name = "Future", StartDate = now.AddDays(1), EndDate = now.AddDays(7) }
        };
        await _dbContext.Promotions.AddRangeAsync(promotions, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.GetActivePromotionsAsync(TestContext.Current.CancellationToken);

        // Assert
        result.Count.ShouldBe(2);
        result.ShouldAllBe(p => p.Name.StartsWith("Active"));
    }

    [Fact]
    public async Task GetActivePromotionsAsync_ShouldReturnEmpty_WhenNoActivePromotions()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var promotions = new[]
        {
            new PromotionReadModel { Id = Guid.NewGuid(), Name = "Past", StartDate = now.AddDays(-30), EndDate = now.AddDays(-1) },
            new PromotionReadModel { Id = Guid.NewGuid(), Name = "Future", StartDate = now.AddDays(1), EndDate = now.AddDays(7) }
        };
        await _dbContext.Promotions.AddRangeAsync(promotions, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.GetActivePromotionsAsync(TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetByCategoryIdAsync_ShouldReturnActivePromotions()
    {
        // Arrange
        var promotions = new[]
        {
            new PromotionReadModel { Id = Guid.NewGuid(), Name = "Promo 1", IsActive = true, StartDate = DateTimeOffset.UtcNow, EndDate = DateTimeOffset.UtcNow.AddDays(7) },
            new PromotionReadModel { Id = Guid.NewGuid(), Name = "Promo 2", IsActive = true, StartDate = DateTimeOffset.UtcNow, EndDate = DateTimeOffset.UtcNow.AddDays(14) },
            new PromotionReadModel { Id = Guid.NewGuid(), Name = "Promo 3", IsActive = false, StartDate = DateTimeOffset.UtcNow, EndDate = DateTimeOffset.UtcNow.AddDays(21) }
        };
        await _dbContext.Promotions.AddRangeAsync(promotions, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.GetByCategoryIdAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        // Assert
        result.Count.ShouldBe(2);
        result.ShouldAllBe(p => p.IsActive);
    }

    [Fact]
    public async Task GetPagedPromotionsAsync_ShouldReturnPagedResults()
    {
        // Arrange
        var baseDate = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var promotions = Enumerable.Range(1, 10)
            .Select(i => new PromotionReadModel 
            { 
                Id = Guid.NewGuid(), 
                Name = $"Promotion {i}",
                StartDate = baseDate.AddDays(i),
                EndDate = baseDate.AddDays(i + 7)
            })
            .ToList();
        await _dbContext.Promotions.AddRangeAsync(promotions, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.GetPagedPromotionsAsync(2, 3, null, TestContext.Current.CancellationToken);

        // Assert
        result.Items.Count.ShouldBe(3);
        result.TotalItems.ShouldBe(10);
        result.Page.ShouldBe(2);
        result.Size.ShouldBe(3);
        result.TotalPages.ShouldBe(4);
    }

    [Fact]
    public async Task GetPagedPromotionsAsync_ShouldFilterByKeyword_InName()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var promotions = new[]
        {
            new PromotionReadModel { Id = Guid.NewGuid(), Name = "Summer Sale", StartDate = now, EndDate = now.AddDays(7) },
            new PromotionReadModel { Id = Guid.NewGuid(), Name = "Winter Sale", StartDate = now, EndDate = now.AddDays(14) },
            new PromotionReadModel { Id = Guid.NewGuid(), Name = "Spring Discount", StartDate = now, EndDate = now.AddDays(21) }
        };
        await _dbContext.Promotions.AddRangeAsync(promotions, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.GetPagedPromotionsAsync(1, 10, "Sale", TestContext.Current.CancellationToken);

        // Assert
        result.Items.Count.ShouldBe(2);
        result.TotalItems.ShouldBe(2);
    }

    [Fact]
    public async Task GetPagedPromotionsAsync_ShouldFilterByKeyword_InDescription()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var promotions = new[]
        {
            new PromotionReadModel { Id = Guid.NewGuid(), Name = "Promo1", Description = "Electronics discount", StartDate = now, EndDate = now.AddDays(7) },
            new PromotionReadModel { Id = Guid.NewGuid(), Name = "Promo2", Description = "Clothing sale", StartDate = now, EndDate = now.AddDays(14) },
            new PromotionReadModel { Id = Guid.NewGuid(), Name = "Promo3", Description = "Electronic items special", StartDate = now, EndDate = now.AddDays(21) }
        };
        await _dbContext.Promotions.AddRangeAsync(promotions, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.GetPagedPromotionsAsync(1, 10, "Electronic", TestContext.Current.CancellationToken);

        // Assert
        result.Items.Count.ShouldBe(2);
    }

    [Fact]
    public async Task GetPagedPromotionsAsync_ShouldReturnAllResults_WhenKeywordIsEmpty()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var promotions = new[]
        {
            new PromotionReadModel { Id = Guid.NewGuid(), Name = "Promo 1", StartDate = now, EndDate = now.AddDays(7) },
            new PromotionReadModel { Id = Guid.NewGuid(), Name = "Promo 2", StartDate = now, EndDate = now.AddDays(14) }
        };
        await _dbContext.Promotions.AddRangeAsync(promotions, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.GetPagedPromotionsAsync(1, 10, "", TestContext.Current.CancellationToken);

        // Assert
        result.Items.Count.ShouldBe(2);
    }

    [Fact]
    public async Task GetPagedPromotionsAsync_ShouldOrderByStartDate()
    {
        // Arrange
        var baseDate = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var promotions = new[]
        {
            new PromotionReadModel { Id = Guid.NewGuid(), Name = "Third", StartDate = baseDate.AddDays(20), EndDate = baseDate.AddDays(27) },
            new PromotionReadModel { Id = Guid.NewGuid(), Name = "First", StartDate = baseDate.AddDays(1), EndDate = baseDate.AddDays(8) },
            new PromotionReadModel { Id = Guid.NewGuid(), Name = "Second", StartDate = baseDate.AddDays(10), EndDate = baseDate.AddDays(17) }
        };
        await _dbContext.Promotions.AddRangeAsync(promotions, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.GetPagedPromotionsAsync(1, 10, null, TestContext.Current.CancellationToken);

        // Assert
        result.Items[0].Name.ShouldBe("First");
        result.Items[1].Name.ShouldBe("Second");
        result.Items[2].Name.ShouldBe("Third");
    }

    public void Dispose()
    {
        _dbContext?.Dispose();
    }
}

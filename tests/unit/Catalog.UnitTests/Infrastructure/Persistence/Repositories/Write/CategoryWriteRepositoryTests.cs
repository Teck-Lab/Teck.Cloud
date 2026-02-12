using Catalog.Domain.Entities.CategoryAggregate;
using Catalog.Infrastructure.Persistence;
using Catalog.Infrastructure.Persistence.Repositories.Write;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;

namespace Catalog.UnitTests.Infrastructure.Persistence.Repositories.Write;

public sealed class CategoryWriteRepositoryTests : IDisposable
{
    private readonly ApplicationWriteDbContext _dbContext;
    private readonly CategoryWriteRepository _repository;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CategoryWriteRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationWriteDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new ApplicationWriteDbContext(options);
        _httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        _repository = new CategoryWriteRepository(_dbContext, _httpContextAccessor);
    }

    [Fact]
    public async Task GetByNameAsync_ShouldReturnCategory_WhenExists()
    {
        // Arrange
        var category = Category.Create("Electronics", "Electronic devices").Value;
        await _dbContext.Categories.AddAsync(category, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.GetByNameAsync("Electronics", TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.Name.ShouldBe("Electronics");
        result.Description.ShouldBe("Electronic devices");
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
        var category = Category.Create("Electronics", "Electronic devices").Value;
        await _dbContext.Categories.AddAsync(category, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.GetByNameAsync("electronics", TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeNull();
    }

    // Note: GetByParentIdAsync tests are skipped because ParentId is a shadow property
    // that requires full EF Core configuration not available in InMemory database.
    // This method is covered by integration tests instead.

    [Fact]
    public async Task AddAsync_ShouldAddCategory()
    {
        // Arrange
        var category = Category.Create("Books", "Books and magazines").Value;

        // Act
        await _repository.AddAsync(category, TestContext.Current.CancellationToken);
        await _repository.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        var result = await _dbContext.Categories.FirstOrDefaultAsync(
            c => c.Name == "Books", 
            TestContext.Current.CancellationToken);
        result.ShouldNotBeNull();
        result.Description.ShouldBe("Books and magazines");
    }

    [Fact]
    public async Task Update_ShouldUpdateCategory()
    {
        // Arrange
        var category = Category.Create("Electronics", "Electronic devices").Value;
        await _dbContext.Categories.AddAsync(category, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        category.Update("Electronics Updated", "Updated description");
        _repository.Update(category);
        await _repository.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        var result = await _dbContext.Categories.FindAsync(new object[] { category.Id }, TestContext.Current.CancellationToken);
        result.ShouldNotBeNull();
        result.Name.ShouldBe("Electronics Updated");
        result.Description.ShouldBe("Updated description");
    }

    [Fact]
    public async Task Delete_ShouldRemoveCategory()
    {
        // Arrange
        var category = Category.Create("ToDelete", "Category to delete").Value;
        await _dbContext.Categories.AddAsync(category, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        _repository.Delete(category);
        await _repository.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        var result = await _dbContext.Categories.FindAsync(new object[] { category.Id }, TestContext.Current.CancellationToken);
        result.ShouldBeNull();
    }

    [Fact]
    public async Task DeleteRange_ShouldRemoveMultipleCategories()
    {
        // Arrange
        var category1 = Category.Create("Category1", "First category").Value;
        var category2 = Category.Create("Category2", "Second category").Value;
        await _dbContext.Categories.AddRangeAsync(new[] { category1, category2 }, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        _repository.DeleteRange(new[] { category1, category2 });
        await _repository.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        var count = await _dbContext.Categories.CountAsync(TestContext.Current.CancellationToken);
        count.ShouldBe(0);
    }

    [Fact]
    public async Task FindByIdAsync_ShouldReturnCategory_WhenExists()
    {
        // Arrange
        var category = Category.Create("Sports", "Sports equipment").Value;
        await _dbContext.Categories.AddAsync(category, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.FindByIdAsync(category.Id, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(category.Id);
        result.Name.ShouldBe("Sports");
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
    public async Task FindByIdsAsync_ShouldReturnMatchingCategories()
    {
        // Arrange
        var category1 = Category.Create("Cat1", "Category 1").Value;
        var category2 = Category.Create("Cat2", "Category 2").Value;
        var category3 = Category.Create("Cat3", "Category 3").Value;
        await _dbContext.Categories.AddRangeAsync(new[] { category1, category2, category3 }, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.FindByIdsAsync(new[] { category1.Id, category3.Id }, TestContext.Current.CancellationToken);

        // Assert
        result.Count.ShouldBe(2);
        result.ShouldContain(c => c.Id == category1.Id);
        result.ShouldContain(c => c.Id == category3.Id);
    }

    [Fact]
    public async Task FindOneAsync_ShouldReturnCategory_WhenMatches()
    {
        // Arrange
        var category = Category.Create("Unique", "Unique category").Value;
        await _dbContext.Categories.AddAsync(category, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.FindOneAsync(c => c.Name == "Unique", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.Name.ShouldBe("Unique");
    }

    [Fact]
    public async Task FindAsync_ShouldReturnMatchingCategories()
    {
        // Arrange
        var category1 = Category.Create("Electronics", "Category 1").Value;
        var category2 = Category.Create("Electronics Accessories", "Category 2").Value;
        var category3 = Category.Create("Books", "Category 3").Value;
        await _dbContext.Categories.AddRangeAsync(new[] { category1, category2, category3 }, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.FindAsync(
            c => c.Name != null && c.Name.Contains("Electronics"), 
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.Count.ShouldBe(2);
        result.ShouldAllBe(c => c.Name != null && c.Name.Contains("Electronics"));
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnAllCategories()
    {
        // Arrange
        var category1 = Category.Create("Cat1", "Category 1").Value;
        var category2 = Category.Create("Cat2", "Category 2").Value;
        await _dbContext.Categories.AddRangeAsync(new[] { category1, category2 }, TestContext.Current.CancellationToken);
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
        var category = Category.Create("Exists", "Existing category").Value;
        await _dbContext.Categories.AddAsync(category, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.ExistsAsync(c => c.Name == "Exists", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public async Task ExistsAsync_ShouldReturnFalse_WhenNotExists()
    {
        // Act
        var result = await _repository.ExistsAsync(c => c.Name == "DoesNotExist", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeFalse();
    }

    public void Dispose()
    {
        _dbContext?.Dispose();
    }
}

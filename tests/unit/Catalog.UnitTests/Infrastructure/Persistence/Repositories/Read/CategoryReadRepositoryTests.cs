using Catalog.Application.Categories.ReadModels;
using Catalog.Infrastructure.Persistence;
using Catalog.Infrastructure.Persistence.Repositories.Read;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Catalog.UnitTests.Infrastructure.Persistence.Repositories.Read;

public sealed class CategoryReadRepositoryTests : IDisposable
{
    private readonly ApplicationReadDbContext _dbContext;
    private readonly CategoryReadRepository _repository;

    public CategoryReadRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationReadDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new ApplicationReadDbContext(options);
        _repository = new CategoryReadRepository(_dbContext);
    }

    [Fact]
    public async Task ExistsByIdAsync_ShouldReturnTrue_WhenAllIdsExist()
    {
        // Arrange
        var category1 = new CategoryReadModel { Id = Guid.NewGuid(), Name = "Category 1" };
        var category2 = new CategoryReadModel { Id = Guid.NewGuid(), Name = "Category 2" };
        await _dbContext.Categories.AddRangeAsync(new[] { category1, category2 }, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.ExistsByIdAsync(new[] { category1.Id, category2.Id }, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public async Task ExistsByIdAsync_ShouldReturnFalse_WhenSomeIdsDontExist()
    {
        // Arrange
        var category1 = new CategoryReadModel { Id = Guid.NewGuid(), Name = "Category 1" };
        await _dbContext.Categories.AddAsync(category1, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.ExistsByIdAsync(
            new[] { category1.Id, Guid.NewGuid() },
            TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public async Task ExistsByIdAsync_ShouldReturnFalse_WhenIdsIsNull()
    {
        // Act
        var result = await _repository.ExistsByIdAsync(null!, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public async Task ExistsByIdAsync_ShouldReturnFalse_WhenIdsIsEmpty()
    {
        // Act
        var result = await _repository.ExistsByIdAsync(Array.Empty<Guid>(), TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public async Task ExistsByIdAsync_ShouldHandleDuplicateIds()
    {
        // Arrange
        var category1 = new CategoryReadModel { Id = Guid.NewGuid(), Name = "Category 1" };
        await _dbContext.Categories.AddAsync(category1, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.ExistsByIdAsync(
            new[] { category1.Id, category1.Id, category1.Id },
            TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnAllCategories()
    {
        // Arrange
        var categories = new[]
        {
            new CategoryReadModel { Id = Guid.NewGuid(), Name = "Category 1" },
            new CategoryReadModel { Id = Guid.NewGuid(), Name = "Category 2" },
            new CategoryReadModel { Id = Guid.NewGuid(), Name = "Category 3" }
        };
        await _dbContext.Categories.AddRangeAsync(categories, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.GetAllAsync(TestContext.Current.CancellationToken);

        // Assert
        result.Count.ShouldBe(3);
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnEmptyList_WhenNoCategories()
    {
        // Act
        var result = await _repository.GetAllAsync(TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnCategory_WhenExists()
    {
        // Arrange
        var category = new CategoryReadModel { Id = Guid.NewGuid(), Name = "Test Category" };
        await _dbContext.Categories.AddAsync(category, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.GetByIdAsync(category.Id, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(category.Id);
        result.Name.ShouldBe("Test Category");
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
    public async Task GetByParentIdAsync_ShouldReturnChildCategories()
    {
        // Arrange
        var parentId = Guid.NewGuid();
        var categories = new[]
        {
            new CategoryReadModel { Id = Guid.NewGuid(), Name = "Child 1", ParentId = parentId },
            new CategoryReadModel { Id = Guid.NewGuid(), Name = "Child 2", ParentId = parentId },
            new CategoryReadModel { Id = Guid.NewGuid(), Name = "Other", ParentId = Guid.NewGuid() }
        };
        await _dbContext.Categories.AddRangeAsync(categories, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.GetByParentIdAsync(parentId, TestContext.Current.CancellationToken);

        // Assert
        result.Count.ShouldBe(2);
        result.ShouldAllBe(c => c.ParentId == parentId);
    }

    [Fact]
    public async Task GetByParentIdAsync_ShouldReturnEmpty_WhenNoChildren()
    {
        // Act
        var result = await _repository.GetByParentIdAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetPagedCategoriesAsync_ShouldReturnPagedResults()
    {
        // Arrange
        var categories = Enumerable.Range(1, 10)
            .Select(i => new CategoryReadModel { Id = Guid.NewGuid(), Name = $"Category {i}" })
            .ToList();
        await _dbContext.Categories.AddRangeAsync(categories, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.GetPagedCategoriesAsync(2, 3, null, TestContext.Current.CancellationToken);

        // Assert
        result.Items.Count.ShouldBe(3);
        result.TotalItems.ShouldBe(10);
        result.Page.ShouldBe(2);
        result.Size.ShouldBe(3);
        result.TotalPages.ShouldBe(4);
    }

    [Fact]
    public async Task GetPagedCategoriesAsync_ShouldFilterByKeyword_InName()
    {
        // Arrange
        var categories = new[]
        {
            new CategoryReadModel { Id = Guid.NewGuid(), Name = "Electronics" },
            new CategoryReadModel { Id = Guid.NewGuid(), Name = "Books" },
            new CategoryReadModel { Id = Guid.NewGuid(), Name = "Electronic Gadgets" }
        };
        await _dbContext.Categories.AddRangeAsync(categories, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.GetPagedCategoriesAsync(1, 10, "Electronic", TestContext.Current.CancellationToken);

        // Assert
        result.Items.Count.ShouldBe(2);
        result.TotalItems.ShouldBe(2);
    }

    [Fact]
    public async Task GetPagedCategoriesAsync_ShouldFilterByKeyword_InDescription()
    {
        // Arrange
        var categories = new[]
        {
            new CategoryReadModel { Id = Guid.NewGuid(), Name = "Cat1", Description = "For electronics" },
            new CategoryReadModel { Id = Guid.NewGuid(), Name = "Cat2", Description = "For books" },
            new CategoryReadModel { Id = Guid.NewGuid(), Name = "Cat3", Description = "Electronic items" }
        };
        await _dbContext.Categories.AddRangeAsync(categories, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.GetPagedCategoriesAsync(1, 10, "electronic", TestContext.Current.CancellationToken);

        // Assert
        result.Items.Count.ShouldBe(1); // Only "For electronics" matches lowercase "electronic"
    }

    [Fact]
    public async Task GetPagedCategoriesAsync_ShouldReturnAllResults_WhenKeywordIsEmpty()
    {
        // Arrange
        var categories = new[]
        {
            new CategoryReadModel { Id = Guid.NewGuid(), Name = "Category 1" },
            new CategoryReadModel { Id = Guid.NewGuid(), Name = "Category 2" }
        };
        await _dbContext.Categories.AddRangeAsync(categories, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.GetPagedCategoriesAsync(1, 10, "", TestContext.Current.CancellationToken);

        // Assert
        result.Items.Count.ShouldBe(2);
    }

    [Fact]
    public async Task GetPagedCategoriesAsync_ShouldOrderByName()
    {
        // Arrange
        var categories = new[]
        {
            new CategoryReadModel { Id = Guid.NewGuid(), Name = "Zebra" },
            new CategoryReadModel { Id = Guid.NewGuid(), Name = "Apple" },
            new CategoryReadModel { Id = Guid.NewGuid(), Name = "Mango" }
        };
        await _dbContext.Categories.AddRangeAsync(categories, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.GetPagedCategoriesAsync(1, 10, null, TestContext.Current.CancellationToken);

        // Assert
        result.Items[0].Name.ShouldBe("Apple");
        result.Items[1].Name.ShouldBe("Mango");
        result.Items[2].Name.ShouldBe("Zebra");
    }

    public void Dispose()
    {
        _dbContext?.Dispose();
    }
}

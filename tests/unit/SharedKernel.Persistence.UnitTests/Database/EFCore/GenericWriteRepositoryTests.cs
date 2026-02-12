using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;
using SharedKernel.Persistence.UnitTests.TestHelpers;

namespace SharedKernel.Persistence.UnitTests.Database.EFCore;

public sealed class GenericWriteRepositoryTests : IAsyncLifetime
{
    private readonly PostgreSqlTestFixture<TestDbContext> _fixture;
    private TestDbContext _dbContext = null!;
    private TestWriteRepository _repository = null!;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly HttpContext _httpContext;

    public GenericWriteRepositoryTests()
    {
        _fixture = new PostgreSqlTestFixture<TestDbContext>();
        _httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        _httpContext = Substitute.For<HttpContext>();
        _httpContextAccessor.HttpContext.Returns(_httpContext);
    }

    public async ValueTask InitializeAsync()
    {
        await _fixture.InitializeAsync();
        
        _dbContext = new TestDbContext(_fixture.CreateDbContextOptions());
        await _dbContext.Database.EnsureCreatedAsync();
        
        _repository = new TestWriteRepository(_dbContext, _httpContextAccessor);
    }

    public async ValueTask DisposeAsync()
    {
        if (_dbContext != null)
        {
            await _dbContext.DisposeAsync();
        }
        
        await _fixture.DisposeAsync();
    }

    #region AddAsync Tests

    [Fact]
    public async Task AddAsync_ShouldAddEntity()
    {
        // Arrange
        var entity = new TestEntity { Name = "Test", Description = "Test Description", Priority = 1 };

        // Act
        await _repository.AddAsync(entity, TestContext.Current.CancellationToken);
        await _repository.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        var result = await _dbContext.TestEntities.FirstOrDefaultAsync(
            e => e.Name == "Test", 
            TestContext.Current.CancellationToken);
        result.ShouldNotBeNull();
        result.Description.ShouldBe("Test Description");
        result.Priority.ShouldBe(1);
    }

    #endregion

    #region Update Tests

    [Fact]
    public async Task Update_ShouldUpdateEntity()
    {
        // Arrange
        var entity = new TestEntity { Name = "Original", Description = "Original Description", Priority = 1 };
        await _dbContext.TestEntities.AddAsync(entity, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        entity.Name = "Updated";
        entity.Description = "Updated Description";
        entity.Priority = 5;
        _repository.Update(entity);
        await _repository.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        var result = await _dbContext.TestEntities.FindAsync(new object[] { entity.Id }, TestContext.Current.CancellationToken);
        result.ShouldNotBeNull();
        result.Name.ShouldBe("Updated");
        result.Description.ShouldBe("Updated Description");
        result.Priority.ShouldBe(5);
    }

    #endregion

    #region Delete Tests

    [Fact]
    public async Task Delete_ShouldRemoveEntity()
    {
        // Arrange
        var entity = new TestEntity { Name = "ToDelete", Description = "Delete me", Priority = 1 };
        await _dbContext.TestEntities.AddAsync(entity, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        _repository.Delete(entity);
        await _repository.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        var result = await _dbContext.TestEntities.FindAsync(new object[] { entity.Id }, TestContext.Current.CancellationToken);
        result.ShouldBeNull();
    }

    [Fact]
    public async Task DeleteRange_ShouldRemoveMultipleEntities()
    {
        // Arrange
        var entity1 = new TestEntity { Name = "Entity1", Priority = 1 };
        var entity2 = new TestEntity { Name = "Entity2", Priority = 2 };
        await _dbContext.TestEntities.AddRangeAsync(new[] { entity1, entity2 }, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        _repository.DeleteRange(new[] { entity1, entity2 });
        await _repository.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        var count = await _dbContext.TestEntities.CountAsync(TestContext.Current.CancellationToken);
        count.ShouldBe(0);
    }

    #endregion

    #region Soft Delete Tests (ExecuteUpdate)

    [Fact]
    public async Task ExcecutSoftDeleteAsync_ShouldSoftDeleteEntity()
    {
        // Arrange
        var entity = new TestEntity { Name = "ToSoftDelete", Priority = 1 };
        await _dbContext.TestEntities.AddAsync(entity, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        var entityId = entity.Id;

        // Act
        await _repository.ExcecutSoftDeleteAsync([entityId], TestContext.Current.CancellationToken);

        // Assert - Need to clear change tracker and query from database
        _dbContext.ChangeTracker.Clear();
        var result = await _dbContext.TestEntities
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(e => e.Id == entityId, TestContext.Current.CancellationToken);
        result.ShouldNotBeNull();
        result.IsDeleted.ShouldBeTrue();
    }

    [Fact]
    public async Task ExcecutSoftDeleteByAsync_ShouldSoftDeleteMatchingEntities()
    {
        // Arrange
        var entity1 = new TestEntity { Name = "Delete1", Priority = 10 };
        var entity2 = new TestEntity { Name = "Delete2", Priority = 10 };
        var entity3 = new TestEntity { Name = "Keep", Priority = 5 };
        await _dbContext.TestEntities.AddRangeAsync(new[] { entity1, entity2, entity3 }, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _repository.ExcecutSoftDeleteByAsync(e => e.Priority == 10, TestContext.Current.CancellationToken);

        // Assert - Need to clear change tracker and query from database
        _dbContext.ChangeTracker.Clear();
        var allEntities = await _dbContext.TestEntities
            .IgnoreQueryFilters()
            .ToListAsync(TestContext.Current.CancellationToken);
        allEntities.Count(e => e.IsDeleted).ShouldBe(2);
        allEntities.Count(e => !e.IsDeleted).ShouldBe(1);
    }

    #endregion

    #region Hard Delete Tests (ExecuteDelete)

    [Fact]
    public async Task ExcecutHardDeleteAsync_ShouldPermanentlyDeleteEntity()
    {
        // Arrange
        var entity = new TestEntity { Name = "ToHardDelete", Priority = 1 };
        await _dbContext.TestEntities.AddAsync(entity, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _repository.ExcecutHardDeleteAsync([entity.Id], TestContext.Current.CancellationToken);

        // Assert
        var result = await _dbContext.TestEntities
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(e => e.Id == entity.Id, TestContext.Current.CancellationToken);
        result.ShouldBeNull();
    }

    #endregion

    #region FindByIdAsync Tests

    [Fact]
    public async Task FindByIdAsync_ShouldReturnEntity_WhenExists()
    {
        // Arrange
        var entity = new TestEntity { Name = "FindMe", Description = "Test", Priority = 10 };
        await _dbContext.TestEntities.AddAsync(entity, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.FindByIdAsync(entity.Id, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(entity.Id);
        result.Name.ShouldBe("FindMe");
    }

    [Fact]
    public async Task FindByIdAsync_ShouldReturnNull_WhenNotExists()
    {
        // Act
        var result = await _repository.FindByIdAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeNull();
    }

    #endregion

    #region FindByIdsAsync Tests

    [Fact]
    public async Task FindByIdsAsync_ShouldReturnMatchingEntities()
    {
        // Arrange
        var entity1 = new TestEntity { Name = "Entity1", Priority = 1 };
        var entity2 = new TestEntity { Name = "Entity2", Priority = 2 };
        var entity3 = new TestEntity { Name = "Entity3", Priority = 3 };
        await _dbContext.TestEntities.AddRangeAsync(new[] { entity1, entity2, entity3 }, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.FindByIdsAsync(new[] { entity1.Id, entity3.Id }, TestContext.Current.CancellationToken);

        // Assert
        result.Count.ShouldBe(2);
        result.ShouldContain(e => e.Id == entity1.Id);
        result.ShouldContain(e => e.Id == entity3.Id);
    }

    [Fact]
    public async Task FindByIdsAsync_ShouldReturnEmpty_WhenNoMatches()
    {
        // Act
        var result = await _repository.FindByIdsAsync(new[] { Guid.NewGuid(), Guid.NewGuid() }, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeEmpty();
    }

    #endregion

    #region FindOneAsync Tests

    [Fact]
    public async Task FindOneAsync_ShouldReturnEntity_WhenMatches()
    {
        // Arrange
        var entity = new TestEntity { Name = "UniqueEntity", Priority = 99 };
        await _dbContext.TestEntities.AddAsync(entity, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.FindOneAsync(e => e.Name == "UniqueEntity", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.Name.ShouldBe("UniqueEntity");
        result.Priority.ShouldBe(99);
    }

    [Fact]
    public async Task FindOneAsync_ShouldReturnNull_WhenNoMatch()
    {
        // Act
        var result = await _repository.FindOneAsync(e => e.Name == "DoesNotExist", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task FindOneAsync_WithTracking_ShouldTrackEntity()
    {
        // Arrange
        var entity = new TestEntity { Name = "TrackMe", Priority = 1 };
        await _dbContext.TestEntities.AddAsync(entity, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        _dbContext.ChangeTracker.Clear();

        // Act
        var result = await _repository.FindOneAsync(e => e.Name == "TrackMe", enableTracking: true, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        var entry = _dbContext.Entry(result);
        entry.State.ShouldBe(EntityState.Unchanged);
    }

    #endregion

    #region FindAsync Tests

    [Fact]
    public async Task FindAsync_ShouldReturnMatchingEntities()
    {
        // Arrange
        var entity1 = new TestEntity { Name = "High Priority", Priority = 10 };
        var entity2 = new TestEntity { Name = "Medium Priority", Priority = 5 };
        var entity3 = new TestEntity { Name = "Low Priority", Priority = 1 };
        await _dbContext.TestEntities.AddRangeAsync(new[] { entity1, entity2, entity3 }, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.FindAsync(e => e.Priority > 3, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.Count.ShouldBe(2);
        result.ShouldAllBe(e => e.Priority > 3);
    }

    [Fact]
    public async Task FindAsync_ShouldReturnEmpty_WhenNoMatches()
    {
        // Act
        var result = await _repository.FindAsync(e => e.Priority > 100, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeEmpty();
    }

    #endregion

    #region GetAllAsync Tests

    [Fact]
    public async Task GetAllAsync_ShouldReturnAllEntities()
    {
        // Arrange
        var entity1 = new TestEntity { Name = "Entity1", Priority = 1 };
        var entity2 = new TestEntity { Name = "Entity2", Priority = 2 };
        await _dbContext.TestEntities.AddRangeAsync(new[] { entity1, entity2 }, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.GetAllAsync(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.Count.ShouldBe(2);
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnEmpty_WhenNoEntities()
    {
        // Act
        var result = await _repository.GetAllAsync(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeEmpty();
    }

    #endregion

    #region ExistsAsync Tests

    [Fact]
    public async Task ExistsAsync_ShouldReturnTrue_WhenEntityExists()
    {
        // Arrange
        var entity = new TestEntity { Name = "Exists", Priority = 1 };
        await _dbContext.TestEntities.AddAsync(entity, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.ExistsAsync(e => e.Name == "Exists", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public async Task ExistsAsync_ShouldReturnFalse_WhenEntityDoesNotExist()
    {
        // Act
        var result = await _repository.ExistsAsync(e => e.Name == "DoesNotExist", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeFalse();
    }

    #endregion

    #region SaveChangesAsync Tests

    [Fact]
    public async Task SaveChangesAsync_ShouldReturnNumberOfAffectedEntities()
    {
        // Arrange
        var entity1 = new TestEntity { Name = "Entity1", Priority = 1 };
        var entity2 = new TestEntity { Name = "Entity2", Priority = 2 };
        await _dbContext.TestEntities.AddRangeAsync(new[] { entity1, entity2 }, TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBe(2);
    }

    #endregion
}

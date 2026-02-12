using Microsoft.EntityFrameworkCore;
using Shouldly;
using SharedKernel.Persistence.UnitTests.TestHelpers;

namespace SharedKernel.Persistence.UnitTests.Database.EFCore;

public sealed class GenericReadRepositoryTests : IAsyncLifetime
{
    private readonly PostgreSqlTestFixture<TestReadDbContext> _fixture;
    private TestReadDbContext _dbContext = null!;
    private TestReadRepository _repository = null!;

    public GenericReadRepositoryTests()
    {
        _fixture = new PostgreSqlTestFixture<TestReadDbContext>();
    }

    public async ValueTask InitializeAsync()
    {
        await _fixture.InitializeAsync();
        
        _dbContext = new TestReadDbContext(_fixture.CreateDbContextOptions());
        await _dbContext.Database.EnsureCreatedAsync();
        
        _repository = new TestReadRepository(_dbContext);
    }

    public async ValueTask DisposeAsync()
    {
        if (_dbContext != null)
        {
            await _dbContext.DisposeAsync();
        }
        
        await _fixture.DisposeAsync();
    }

    #region ExistsAsync Tests

    [Fact]
    public async Task ExistsAsync_ShouldReturnTrue_WhenEntityExists()
    {
        // Arrange
        var readModel = new TestReadModel { Id = Guid.NewGuid(), Name = "Exists", Priority = 1 };
        await _dbContext.TestReadModels.AddAsync(readModel, TestContext.Current.CancellationToken);
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

    #region FindAsync Tests

    [Fact]
    public async Task FindAsync_ShouldReturnMatchingEntities()
    {
        // Arrange
        var readModel1 = new TestReadModel { Id = Guid.NewGuid(), Name = "High Priority", Priority = 10 };
        var readModel2 = new TestReadModel { Id = Guid.NewGuid(), Name = "Medium Priority", Priority = 5 };
        var readModel3 = new TestReadModel { Id = Guid.NewGuid(), Name = "Low Priority", Priority = 1 };
        await _dbContext.TestReadModels.AddRangeAsync(new[] { readModel1, readModel2, readModel3 }, TestContext.Current.CancellationToken);
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

    #region FindByIdAsync Tests

    [Fact]
    public async Task FindByIdAsync_ShouldReturnEntity_WhenExists()
    {
        // Arrange
        var readModel = new TestReadModel { Id = Guid.NewGuid(), Name = "FindMe", Priority = 10 };
        await _dbContext.TestReadModels.AddAsync(readModel, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.FindByIdAsync(readModel.Id, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(readModel.Id);
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
        var readModel1 = new TestReadModel { Id = Guid.NewGuid(), Name = "Entity1", Priority = 1 };
        var readModel2 = new TestReadModel { Id = Guid.NewGuid(), Name = "Entity2", Priority = 2 };
        var readModel3 = new TestReadModel { Id = Guid.NewGuid(), Name = "Entity3", Priority = 3 };
        await _dbContext.TestReadModels.AddRangeAsync(new[] { readModel1, readModel2, readModel3 }, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.FindByIdsAsync(new[] { readModel1.Id, readModel3.Id }, TestContext.Current.CancellationToken);

        // Assert
        result.Count.ShouldBe(2);
        result.ShouldContain(e => e.Id == readModel1.Id);
        result.ShouldContain(e => e.Id == readModel3.Id);
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
        var readModel = new TestReadModel { Id = Guid.NewGuid(), Name = "UniqueEntity", Priority = 99 };
        await _dbContext.TestReadModels.AddAsync(readModel, TestContext.Current.CancellationToken);
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
        var readModel = new TestReadModel { Id = Guid.NewGuid(), Name = "TrackMe", Priority = 1 };
        await _dbContext.TestReadModels.AddAsync(readModel, TestContext.Current.CancellationToken);
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

    #region GetAllAsync Tests

    [Fact]
    public async Task GetAllAsync_ShouldReturnAllEntities()
    {
        // Arrange
        var readModel1 = new TestReadModel { Id = Guid.NewGuid(), Name = "Entity1", Priority = 1 };
        var readModel2 = new TestReadModel { Id = Guid.NewGuid(), Name = "Entity2", Priority = 2 };
        await _dbContext.TestReadModels.AddRangeAsync(new[] { readModel1, readModel2 }, TestContext.Current.CancellationToken);
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

    #region FirstOrDefaultAsync (Specification) Tests

    [Fact]
    public async Task FirstOrDefaultAsync_WithSpecification_ShouldReturnMatchingEntity()
    {
        // Arrange
        var readModel = new TestReadModel { Id = Guid.NewGuid(), Name = "TestEntity", Priority = 5 };
        await _dbContext.TestReadModels.AddAsync(readModel, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var spec = new TestByNameSpecification("TestEntity");

        // Act
        var result = await _repository.FirstOrDefaultAsync(spec, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.Name.ShouldBe("TestEntity");
    }

    [Fact]
    public async Task FirstOrDefaultAsync_WithSpecification_ShouldReturnNull_WhenNoMatch()
    {
        // Arrange
        var spec = new TestByNameSpecification("DoesNotExist");

        // Act
        var result = await _repository.FirstOrDefaultAsync(spec, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task FirstOrDefaultAsync_WithSpecificationAndTracking_ShouldTrackEntity()
    {
        // Arrange
        var readModel = new TestReadModel { Id = Guid.NewGuid(), Name = "TrackMe", Priority = 1 };
        await _dbContext.TestReadModels.AddAsync(readModel, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        _dbContext.ChangeTracker.Clear();

        var spec = new TestByNameSpecification("TrackMe");

        // Act
        var result = await _repository.FirstOrDefaultAsync(spec, enableTracking: true, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        var entry = _dbContext.Entry(result);
        entry.State.ShouldBe(EntityState.Unchanged);
    }

    [Fact]
    public async Task FirstOrDefaultAsync_WithProjection_ShouldReturnProjectedResult()
    {
        // Arrange
        var readModel = new TestReadModel { Id = Guid.NewGuid(), Name = "ProjectMe", Priority = 10, Description = "Description" };
        await _dbContext.TestReadModels.AddAsync(readModel, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var spec = new TestNamePriorityProjectionSpecification();

        // Act
        var result = await _repository.FirstOrDefaultAsync(spec, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.Name.ShouldBe("ProjectMe");
        result.Priority.ShouldBe(10);
    }

    #endregion

    #region ListAsync (Specification) Tests

    [Fact]
    public async Task ListAsync_WithSpecification_ShouldReturnMatchingEntities()
    {
        // Arrange
        var readModel1 = new TestReadModel { Id = Guid.NewGuid(), Name = "Entity1", Priority = 10 };
        var readModel2 = new TestReadModel { Id = Guid.NewGuid(), Name = "Entity2", Priority = 5 };
        var readModel3 = new TestReadModel { Id = Guid.NewGuid(), Name = "Entity3", Priority = 1 };
        await _dbContext.TestReadModels.AddRangeAsync(new[] { readModel1, readModel2, readModel3 }, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var spec = new TestByPrioritySpecification(5);

        // Act
        var result = await _repository.ListAsync(spec, TestContext.Current.CancellationToken);

        // Assert
        result.Count.ShouldBe(2);
        result.ShouldAllBe(e => e.Priority >= 5);
    }

    [Fact]
    public async Task ListAsync_WithSpecification_ShouldReturnEmpty_WhenNoMatches()
    {
        // Arrange
        var spec = new TestByPrioritySpecification(100);

        // Act
        var result = await _repository.ListAsync(spec, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task ListAsync_WithSpecificationAndTracking_ShouldTrackEntities()
    {
        // Arrange
        var readModel1 = new TestReadModel { Id = Guid.NewGuid(), Name = "Entity1", Priority = 10 };
        var readModel2 = new TestReadModel { Id = Guid.NewGuid(), Name = "Entity2", Priority = 8 };
        await _dbContext.TestReadModels.AddRangeAsync(new[] { readModel1, readModel2 }, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        _dbContext.ChangeTracker.Clear();

        var spec = new TestByPrioritySpecification(5);

        // Act
        var result = await _repository.ListAsync(spec, enableTracking: true, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.Count.ShouldBe(2);
        foreach (var entity in result)
        {
            var entry = _dbContext.Entry(entity);
            entry.State.ShouldBe(EntityState.Unchanged);
        }
    }

    [Fact]
    public async Task ListAsync_WithProjection_ShouldReturnProjectedResults()
    {
        // Arrange
        var readModel1 = new TestReadModel { Id = Guid.NewGuid(), Name = "Entity1", Priority = 10, Description = "Desc1" };
        var readModel2 = new TestReadModel { Id = Guid.NewGuid(), Name = "Entity2", Priority = 5, Description = "Desc2" };
        await _dbContext.TestReadModels.AddRangeAsync(new[] { readModel1, readModel2 }, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var spec = new TestNamePriorityProjectionSpecification();

        // Act
        var result = await _repository.ListAsync(spec, TestContext.Current.CancellationToken);

        // Assert
        result.Count.ShouldBe(2);
        result.ShouldAllBe(dto => !string.IsNullOrEmpty(dto.Name));
        result.ShouldAllBe(dto => dto.Priority > 0);
    }

    #endregion

    #region CountAsync Tests

    [Fact]
    public async Task CountAsync_WithSpecification_ShouldReturnCount()
    {
        // Arrange
        var readModel1 = new TestReadModel { Id = Guid.NewGuid(), Name = "Entity1", Priority = 10 };
        var readModel2 = new TestReadModel { Id = Guid.NewGuid(), Name = "Entity2", Priority = 8 };
        var readModel3 = new TestReadModel { Id = Guid.NewGuid(), Name = "Entity3", Priority = 3 };
        await _dbContext.TestReadModels.AddRangeAsync(new[] { readModel1, readModel2, readModel3 }, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var spec = new TestByPrioritySpecification(5);

        // Act
        var result = await _repository.CountAsync(spec, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBe(2);
    }

    [Fact]
    public async Task CountAsync_WithSpecification_ShouldReturnZero_WhenNoMatches()
    {
        // Arrange
        var spec = new TestByPrioritySpecification(100);

        // Act
        var result = await _repository.CountAsync(spec, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBe(0);
    }

    #endregion

    #region AnyAsync Tests

    [Fact]
    public async Task AnyAsync_WithSpecification_ShouldReturnTrue_WhenMatches()
    {
        // Arrange
        var readModel = new TestReadModel { Id = Guid.NewGuid(), Name = "Entity", Priority = 10 };
        await _dbContext.TestReadModels.AddAsync(readModel, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var spec = new TestByPrioritySpecification(5);

        // Act
        var result = await _repository.AnyAsync(spec, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public async Task AnyAsync_WithSpecification_ShouldReturnFalse_WhenNoMatches()
    {
        // Arrange
        var spec = new TestByPrioritySpecification(100);

        // Act
        var result = await _repository.AnyAsync(spec, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeFalse();
    }

    #endregion
}

using Catalog.Application.Suppliers.ReadModels;
using Catalog.Infrastructure.Persistence;
using Catalog.Infrastructure.Persistence.Repositories.Read;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Catalog.UnitTests.Infrastructure.Persistence.Repositories.Read;

public sealed class SupplierReadRepositoryTests : IDisposable
{
    private readonly ApplicationReadDbContext _dbContext;
    private readonly SupplierReadRepository _repository;

    public SupplierReadRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationReadDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new ApplicationReadDbContext(options);
        _repository = new SupplierReadRepository(_dbContext);
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnAllSuppliers()
    {
        // Arrange
        var suppliers = new[]
        {
            new SupplierReadModel { Id = Guid.NewGuid(), Name = "Supplier 1" },
            new SupplierReadModel { Id = Guid.NewGuid(), Name = "Supplier 2" },
            new SupplierReadModel { Id = Guid.NewGuid(), Name = "Supplier 3" }
        };
        await _dbContext.Suppliers.AddRangeAsync(suppliers, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.GetAllAsync(TestContext.Current.CancellationToken);

        // Assert
        result.Count.ShouldBe(3);
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnEmptyList_WhenNoSuppliers()
    {
        // Act
        var result = await _repository.GetAllAsync(TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnSupplier_WhenExists()
    {
        // Arrange
        var supplier = new SupplierReadModel { Id = Guid.NewGuid(), Name = "Test Supplier" };
        await _dbContext.Suppliers.AddAsync(supplier, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.GetByIdAsync(supplier.Id, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(supplier.Id);
        result.Name.ShouldBe("Test Supplier");
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
    public async Task GetPagedSuppliersAsync_ShouldReturnPagedResults()
    {
        // Arrange
        var suppliers = Enumerable.Range(1, 10)
            .Select(i => new SupplierReadModel { Id = Guid.NewGuid(), Name = $"Supplier {i}" })
            .ToList();
        await _dbContext.Suppliers.AddRangeAsync(suppliers, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.GetPagedSuppliersAsync(2, 3, null, TestContext.Current.CancellationToken);

        // Assert
        result.Items.Count.ShouldBe(3);
        result.TotalItems.ShouldBe(10);
        result.Page.ShouldBe(2);
        result.Size.ShouldBe(3);
        result.TotalPages.ShouldBe(4);
    }

    [Fact]
    public async Task GetPagedSuppliersAsync_ShouldFilterByKeyword_InName()
    {
        // Arrange
        var suppliers = new[]
        {
            new SupplierReadModel { Id = Guid.NewGuid(), Name = "Global Supplies Inc" },
            new SupplierReadModel { Id = Guid.NewGuid(), Name = "Local Parts" },
            new SupplierReadModel { Id = Guid.NewGuid(), Name = "Global Trade Co" }
        };
        await _dbContext.Suppliers.AddRangeAsync(suppliers, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.GetPagedSuppliersAsync(1, 10, "Global", TestContext.Current.CancellationToken);

        // Assert
        result.Items.Count.ShouldBe(2);
        result.TotalItems.ShouldBe(2);
    }

    [Fact]
    public async Task GetPagedSuppliersAsync_ShouldFilterByKeyword_InDescription()
    {
        // Arrange
        var suppliers = new[]
        {
            new SupplierReadModel { Id = Guid.NewGuid(), Name = "Sup1", Description = "electronics supplier" },
            new SupplierReadModel { Id = Guid.NewGuid(), Name = "Sup2", Description = "furniture provider" },
            new SupplierReadModel { Id = Guid.NewGuid(), Name = "Sup3", Description = "supplier of electronics" }
        };
        await _dbContext.Suppliers.AddRangeAsync(suppliers, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.GetPagedSuppliersAsync(1, 10, "electronics", TestContext.Current.CancellationToken);

        // Assert
        result.Items.Count.ShouldBe(2);
    }

    [Fact]
    public async Task GetPagedSuppliersAsync_ShouldReturnAllResults_WhenKeywordIsEmpty()
    {
        // Arrange
        var suppliers = new[]
        {
            new SupplierReadModel { Id = Guid.NewGuid(), Name = "Supplier 1" },
            new SupplierReadModel { Id = Guid.NewGuid(), Name = "Supplier 2" }
        };
        await _dbContext.Suppliers.AddRangeAsync(suppliers, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.GetPagedSuppliersAsync(1, 10, "", TestContext.Current.CancellationToken);

        // Assert
        result.Items.Count.ShouldBe(2);
    }

    [Fact]
    public async Task GetPagedSuppliersAsync_ShouldOrderByName()
    {
        // Arrange
        var suppliers = new[]
        {
            new SupplierReadModel { Id = Guid.NewGuid(), Name = "Zebra Supplies" },
            new SupplierReadModel { Id = Guid.NewGuid(), Name = "Alpha Trading" },
            new SupplierReadModel { Id = Guid.NewGuid(), Name = "Mango Corp" }
        };
        await _dbContext.Suppliers.AddRangeAsync(suppliers, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.GetPagedSuppliersAsync(1, 10, null, TestContext.Current.CancellationToken);

        // Assert
        result.Items[0].Name.ShouldBe("Alpha Trading");
        result.Items[1].Name.ShouldBe("Mango Corp");
        result.Items[2].Name.ShouldBe("Zebra Supplies");
    }

    public void Dispose()
    {
        _dbContext?.Dispose();
    }
}

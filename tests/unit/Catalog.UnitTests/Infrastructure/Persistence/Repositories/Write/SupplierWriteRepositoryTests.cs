using Catalog.Domain.Entities.SupplierAggregate;
using Catalog.Infrastructure.Persistence;
using Catalog.Infrastructure.Persistence.Repositories.Write;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;

namespace Catalog.UnitTests.Infrastructure.Persistence.Repositories.Write;

public sealed class SupplierWriteRepositoryTests : IDisposable
{
    private readonly ApplicationWriteDbContext _dbContext;
    private readonly SupplierWriteRepository _repository;

    public SupplierWriteRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationWriteDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new ApplicationWriteDbContext(options, Catalog.UnitTests.Infrastructure.Persistence.TestTenantContextAccessor.Create());
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        _repository = new SupplierWriteRepository(_dbContext, httpContextAccessor);
    }

    [Fact]
    public async Task GetByNameAsync_ShouldReturnSupplier_WhenExists()
    {
        // Arrange
        var supplierResult = Supplier.Create("Northwind", "Primary supplier", "https://northwind.example.com");
        supplierResult.IsError.ShouldBeFalse();

        await _dbContext.Suppliers.AddAsync(supplierResult.Value, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.GetByNameAsync("Northwind", TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.Name.ShouldBe("Northwind");
        result.Description.ShouldBe("Primary supplier");
    }

    [Fact]
    public async Task GetByNameAsync_ShouldReturnNull_WhenNotFound()
    {
        // Act
        var result = await _repository.GetByNameAsync("Missing", TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeNull();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}

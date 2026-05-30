// <copyright file="ProductWriteRepositoryTests.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Microsoft.EntityFrameworkCore;
using Product.Infrastructure.Persistence;
using Product.Infrastructure.Persistence.Repositories.Write;
using ProductEntity = global::Product.Domain.Entities.ProductAggregate.Product;
using Shouldly;

namespace Product.IntegrationTests.Infrastructure.Persistence.Repositories.Write;

public sealed class ProductWriteRepositoryTests : IAsyncLifetime
{
    private ProductWriteDbContext _dbContext = null!;
    private DbProductWriteRepository _repository = null!;

    public async ValueTask InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<ProductWriteDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new ProductWriteDbContext(options);
        await _dbContext.Database.EnsureCreatedAsync();

        _repository = new DbProductWriteRepository(_dbContext, null!);
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
    }

    [Fact]
    public async Task AddAsync_ShouldPersistProduct()
    {
        var product = ProductEntity.Create("Mouse", "SKU-001", "12345").Value;

        await _repository.AddAsync(product, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var persisted = await _dbContext.Products.FindAsync([product.Id], TestContext.Current.CancellationToken);
        persisted.ShouldNotBeNull();
        persisted.Name.ShouldBe("Mouse");
        persisted.SKU.ShouldBe("SKU-001");
    }

    [Fact]
    public async Task ExistsBySkuAsync_ShouldReturnTrue_WhenProductExists()
    {
        var product = ProductEntity.Create("Keyboard", "SKU-002").Value;
        await _repository.AddAsync(product, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        bool exists = await _repository.ExistsBySkuAsync("SKU-002", TestContext.Current.CancellationToken);

        exists.ShouldBeTrue();
    }

    [Fact]
    public async Task ExistsBySkuAsync_ShouldReturnFalse_WhenProductDoesNotExist()
    {
        bool exists = await _repository.ExistsBySkuAsync("NON-EXISTENT", TestContext.Current.CancellationToken);

        exists.ShouldBeFalse();
    }
}

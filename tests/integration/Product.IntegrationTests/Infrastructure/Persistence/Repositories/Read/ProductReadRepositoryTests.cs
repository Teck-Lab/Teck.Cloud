// <copyright file="ProductReadRepositoryTests.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Microsoft.EntityFrameworkCore;
using Product.Application.Product.Features.GetProducts.V1;
using Product.Infrastructure.Persistence;
using Product.Infrastructure.Persistence.Repositories.Read;
using SharedKernel.Core.Pagination;
using Shouldly;

namespace Product.IntegrationTests.Infrastructure.Persistence.Repositories.Read;

public sealed class ProductReadRepositoryTests : IAsyncLifetime
{
    private ProductWriteDbContext _writeDbContext = null!;
    private ProductReadDbContext _readDbContext = null!;
    private DbProductReadRepository _repository = null!;

    public async ValueTask InitializeAsync()
    {
        var writeOptions = new DbContextOptionsBuilder<ProductWriteDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var readOptions = new DbContextOptionsBuilder<ProductReadDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _writeDbContext = new ProductWriteDbContext(writeOptions);
        _readDbContext = new ProductReadDbContext(readOptions);
        await _writeDbContext.Database.EnsureCreatedAsync();
        await _readDbContext.Database.EnsureCreatedAsync();

        _repository = new DbProductReadRepository(_readDbContext);
    }

    public async ValueTask DisposeAsync()
    {
        await _writeDbContext.DisposeAsync();
        await _readDbContext.DisposeAsync();
    }

    [Fact]
    public async Task GetPagedAsync_ShouldReturnEmpty_WhenNoProducts()
    {
        PagedList<GetProductItemResponse> result = await _repository.GetPagedAsync(1, 10, null, false, TestContext.Current.CancellationToken);

        result.Items.ShouldBeEmpty();
        result.TotalItems.ShouldBe(0);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnNull_WhenProductDoesNotExist()
    {
        GetProductItemResponse? result = await _repository.GetByIdAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }
}

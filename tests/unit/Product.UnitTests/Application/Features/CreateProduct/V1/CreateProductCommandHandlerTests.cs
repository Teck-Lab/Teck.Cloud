// <copyright file="CreateProductCommandHandlerTests.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using NSubstitute;
using Product.Application.Product.Abstractions;
using Product.Application.Product.Features.CreateProduct.V1;
using Product.Domain.Entities.ProductAggregate.Errors;
using ProductEntity = global::Product.Domain.Entities.ProductAggregate.Product;
using SharedKernel.Core.Database;
using Shouldly;

namespace Product.UnitTests.Application.Features.CreateProduct.V1;

public sealed class CreateProductCommandHandlerTests
{
    [Fact]
    public async Task Handle_ShouldReturnProductId_WhenCreated()
    {
        var writeRepository = Substitute.For<IProductWriteRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var handler = new CreateProductCommandHandler(writeRepository, unitOfWork);
        var command = new CreateProductCommand("Mouse", "SKU-001", "12345");

        writeRepository.ExistsBySkuAsync(command.Sku, Arg.Any<CancellationToken>()).Returns(false);
        writeRepository.AddAsync(Arg.Any<ProductEntity>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsError.ShouldBeFalse();
        result.Value.ProductId.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task Handle_ShouldReturnDuplicateError_WhenSkuExists()
    {
        var writeRepository = Substitute.For<IProductWriteRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var handler = new CreateProductCommandHandler(writeRepository, unitOfWork);
        var command = new CreateProductCommand("Mouse", "SKU-001", null);

        writeRepository.ExistsBySkuAsync(command.Sku, Arg.Any<CancellationToken>()).Returns(true);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsError.ShouldBeTrue();
        result.FirstError.ShouldBe(ProductErrors.DuplicateSku);
        await writeRepository.DidNotReceive().AddAsync(Arg.Any<ProductEntity>(), Arg.Any<CancellationToken>());
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}

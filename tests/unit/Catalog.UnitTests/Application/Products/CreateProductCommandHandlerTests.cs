using Catalog.Application.Products.Features.CreateProduct.V1;
using Catalog.Domain.Entities.CategoryAggregate.Repositories;
using Catalog.Domain.Entities.ProductAggregate;
using Catalog.Domain.Entities.ProductAggregate.Errors;
using Catalog.Domain.Entities.ProductAggregate.Repositories;
using NSubstitute;
using SharedKernel.Core.Database;

namespace Catalog.UnitTests.Application.Products
{
    public class CreateProductCommandHandlerTests
    {
        [Fact]
        public async Task Handle_Should_ReturnProductResponse_WhenProductIsCreated()
        {
            var unitOfWork = Substitute.For<IUnitOfWork>();
            var productWriteRepository = Substitute.For<IProductWriteRepository>();
            var categoryWriteRepository = Substitute.For<ICategoryWriteRepository>();
            var handler = new CreateProductCommandHandler(unitOfWork, productWriteRepository, categoryWriteRepository);
            var command = new CreateProductCommand("Test Product", "desc", "sku", "gtin", Guid.NewGuid(), new List<Guid> { Guid.NewGuid() }, true);
            productWriteRepository.AddAsync(Arg.Any<Product>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
            unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);
            var result = await handler.Handle(command, CancellationToken.None);
            Assert.False(result.IsError);
        }

        [Fact]
        public async Task Handle_Should_ReturnError_WhenProductNameIsEmpty()
        {
            var unitOfWork = Substitute.For<IUnitOfWork>();
            var productWriteRepository = Substitute.For<IProductWriteRepository>();
            var categoryWriteRepository = Substitute.For<ICategoryWriteRepository>();
            var handler = new CreateProductCommandHandler(unitOfWork, productWriteRepository, categoryWriteRepository);
            var command = new CreateProductCommand("", "desc", "sku", "gtin", Guid.NewGuid(), new List<Guid>(), true);
            var result = await handler.Handle(command, CancellationToken.None);
            Assert.True(result.IsError);
            Assert.Equal(ProductErrors.EmptyName, result.FirstError);
            await productWriteRepository.DidNotReceive().AddAsync(Arg.Any<Product>(), Arg.Any<CancellationToken>());
            await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_Should_ReturnError_WhenProductSkuIsEmpty()
        {
            var unitOfWork = Substitute.For<IUnitOfWork>();
            var productWriteRepository = Substitute.For<IProductWriteRepository>();
            var categoryWriteRepository = Substitute.For<ICategoryWriteRepository>();
            var handler = new CreateProductCommandHandler(unitOfWork, productWriteRepository, categoryWriteRepository);
            var command = new CreateProductCommand("Test Product", "desc", "", "gtin", null, new List<Guid>(), true);
            var result = await handler.Handle(command, CancellationToken.None);
            Assert.True(result.IsError);
            Assert.Equal(ProductErrors.EmptySKU, result.FirstError);
            await productWriteRepository.DidNotReceive().AddAsync(Arg.Any<Product>(), Arg.Any<CancellationToken>());
            await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_Should_ReturnNotCreated_WhenSaveChangesReturnsZero()
        {
            var unitOfWork = Substitute.For<IUnitOfWork>();
            var productWriteRepository = Substitute.For<IProductWriteRepository>();
            var categoryWriteRepository = Substitute.For<ICategoryWriteRepository>();
            var handler = new CreateProductCommandHandler(unitOfWork, productWriteRepository, categoryWriteRepository);
            var command = new CreateProductCommand("Test Product", "desc", "sku", "gtin", Guid.NewGuid(), new List<Guid> { Guid.NewGuid() }, true);
            productWriteRepository.AddAsync(Arg.Any<Product>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
            unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(0);
            var result = await handler.Handle(command, CancellationToken.None);
            Assert.True(result.IsError);
            Assert.Equal(ProductErrors.NotCreated, result.FirstError);
        }

        [Fact]
        public async Task Handle_Should_Throw_WhenCategoryRepositoryThrows()
        {
            // Arrange
            var unitOfWork = Substitute.For<IUnitOfWork>();
            var productWriteRepository = Substitute.For<IProductWriteRepository>();
            var categoryWriteRepository = Substitute.For<ICategoryWriteRepository>();
            var handler = new CreateProductCommandHandler(unitOfWork, productWriteRepository, categoryWriteRepository);
            var command = new CreateProductCommand("Test Product", "desc", "sku", "gtin", Guid.NewGuid(), new List<Guid> { Guid.NewGuid() }, true);

            categoryWriteRepository
                .When(repo => repo.ListAsync(
                    Arg.Any<Catalog.Domain.Entities.CategoryAggregate.Specifications.CategoriesByIdsSpecification>(),
                    Arg.Any<bool>(),
                    Arg.Any<CancellationToken>()))
                .Do(_ => throw new InvalidOperationException("Category list failure"));

            // Act + Assert
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await handler.Handle(command, CancellationToken.None));
            await productWriteRepository.DidNotReceive().AddAsync(Arg.Any<Product>(), Arg.Any<CancellationToken>());
            await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_Should_Throw_WhenProductRepositoryAddFails()
        {
            // Arrange
            var unitOfWork = Substitute.For<IUnitOfWork>();
            var productWriteRepository = Substitute.For<IProductWriteRepository>();
            var categoryWriteRepository = Substitute.For<ICategoryWriteRepository>();
            var handler = new CreateProductCommandHandler(unitOfWork, productWriteRepository, categoryWriteRepository);
            var command = new CreateProductCommand("Test Product", "desc", "sku", "gtin", Guid.NewGuid(), new List<Guid> { Guid.NewGuid() }, true);

            categoryWriteRepository
                .ListAsync(Arg.Any<Catalog.Domain.Entities.CategoryAggregate.Specifications.CategoriesByIdsSpecification>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
                .Returns(new List<Catalog.Domain.Entities.CategoryAggregate.Category>());

            productWriteRepository
                .AddAsync(Arg.Any<Product>(), Arg.Any<CancellationToken>())
                .Returns(_ => throw new InvalidOperationException("Add failure"));

            // Act + Assert
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await handler.Handle(command, CancellationToken.None));
            await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        }
    }
}

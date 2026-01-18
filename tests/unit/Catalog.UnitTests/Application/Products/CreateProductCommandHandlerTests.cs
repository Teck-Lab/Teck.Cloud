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
    }
}

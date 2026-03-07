using Catalog.Application.Products.Features.GetProductById.V1;
using Catalog.Application.Products.ReadModels;
using Catalog.Application.Products.Repositories;
using Catalog.Domain.Entities.ProductAggregate.Errors;
using NSubstitute;

namespace Catalog.UnitTests.Application.Products
{
    public class GetProductByIdQueryHandlerTests
    {
        [Fact]
        public async Task Handle_Should_ReturnProductResponse_WhenProductExists()
        {
            var productReadRepository = Substitute.For<IProductReadRepository>();
            var handler = new GetProductByIdQueryHandler(productReadRepository);
            var product = new ProductReadModel();
            var query = new GetProductByIdQuery(Guid.NewGuid());
            productReadRepository.GetByIdAsync(query.Id, Arg.Any<CancellationToken>()).Returns(product);
            var result = await handler.Handle(query, CancellationToken.None);
            Assert.False(result.IsError);
        }

        [Fact]
        public async Task Handle_Should_MapExpectedFields_WhenProductExists()
        {
            // Arrange
            var productReadRepository = Substitute.For<IProductReadRepository>();
            var handler = new GetProductByIdQueryHandler(productReadRepository);
            var productId = Guid.NewGuid();
            var query = new GetProductByIdQuery(productId);
            var product = new ProductReadModel
            {
                Id = productId,
                Name = "Product A",
                Description = "Product description",
            };

            productReadRepository.GetByIdAsync(query.Id, Arg.Any<CancellationToken>()).Returns(product);

            // Act
            var result = await handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.False(result.IsError);
            Assert.Equal(productId, result.Value.Id);
            Assert.Equal("Product A", result.Value.Name);
            Assert.Equal("Product description", result.Value.Description);
        }

        [Fact]
        public async Task Handle_Should_ReturnNotFound_WhenProductDoesNotExist()
        {
            var productReadRepository = Substitute.For<IProductReadRepository>();
            var handler = new GetProductByIdQueryHandler(productReadRepository);
            var query = new GetProductByIdQuery(Guid.NewGuid());
            productReadRepository.GetByIdAsync(query.Id, Arg.Any<CancellationToken>()).Returns((ProductReadModel?)null);
            var result = await handler.Handle(query, CancellationToken.None);
            Assert.True(result.IsError);
            Assert.Equal(ProductErrors.NotFound, result.FirstError);
        }

        [Fact]
        public async Task Handle_Should_CallRepositoryWithQueryId_WhenExecuting()
        {
            // Arrange
            var productReadRepository = Substitute.For<IProductReadRepository>();
            var handler = new GetProductByIdQueryHandler(productReadRepository);
            var query = new GetProductByIdQuery(Guid.NewGuid());
            productReadRepository.GetByIdAsync(query.Id, Arg.Any<CancellationToken>()).Returns(new ProductReadModel());

            // Act
            _ = await handler.Handle(query, CancellationToken.None);

            // Assert
            await productReadRepository.Received(1).GetByIdAsync(query.Id, Arg.Any<CancellationToken>());
        }
    }
}

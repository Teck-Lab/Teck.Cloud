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
            var cache = Substitute.For<IProductCache>();
            var handler = new GetProductByIdQueryHandler(cache);
            var product = new ProductReadModel();
            var query = new GetProductByIdQuery(Guid.NewGuid());
            cache.GetOrSetByIdAsync(query.Id, Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(product);
            var result = await handler.Handle(query, CancellationToken.None);
            Assert.False(result.IsError);
        }

        [Fact]
        public async Task Handle_Should_ReturnNotFound_WhenProductDoesNotExist()
        {
            var cache = Substitute.For<IProductCache>();
            var handler = new GetProductByIdQueryHandler(cache);
            var query = new GetProductByIdQuery(Guid.NewGuid());
            cache.GetOrSetByIdAsync(query.Id, Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns((ProductReadModel?)null);
            var result = await handler.Handle(query, CancellationToken.None);
            Assert.True(result.IsError);
            Assert.Equal(ProductErrors.NotFound, result.FirstError);
        }
    }
}

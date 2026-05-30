using Basket.Application.Basket.Features.GetBasketById.V1;
using Basket.Application.Basket.Repositories;
using Basket.Domain.Entities.BasketAggregate;
using ErrorOr;
using NSubstitute;
using Shouldly;

namespace Basket.UnitTests.Application.Features.GetBasketById.V1;

public sealed class GetBasketByIdQueryHandlerTests
{
    [Fact]
    public async Task Handle_WhenBasketFound_ShouldReturnResponse()
    {
        IBasketRepository basketRepository = Substitute.For<IBasketRepository>();
        GetBasketByIdQueryHandler handler = new(basketRepository);

        Guid basketId = Guid.NewGuid();
        GetBasketByIdQuery query = new(basketId, Guid.NewGuid(), Guid.NewGuid(), true);
        BasketDraft basket = BasketDraft.Create(query.TenantId, query.CustomerId);
        basket.AddOrUpdateLine(Guid.NewGuid(), 2, 10m, "USD");

        basketRepository.GetByIdAsync(query.BasketId, query.TenantId, query.CustomerId, query.IsSignedIn, Arg.Any<CancellationToken>())
            .Returns(basket);

        ErrorOr<global::Basket.Application.Basket.Features.AddItemToBasket.V1.AddItemToBasketResponse> result = await handler.Handle(query, CancellationToken.None);

        result.IsError.ShouldBeFalse();
        result.Value.BasketId.ShouldBe(basket.Id);
        result.Value.TotalQuantity.ShouldBe(2);
    }

    [Fact]
    public async Task Handle_WhenBasketNotFound_ShouldReturnNotFound()
    {
        IBasketRepository basketRepository = Substitute.For<IBasketRepository>();
        GetBasketByIdQueryHandler handler = new(basketRepository);
        GetBasketByIdQuery query = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), false);

        basketRepository.GetByIdAsync(query.BasketId, query.TenantId, query.CustomerId, query.IsSignedIn, Arg.Any<CancellationToken>())
            .Returns((BasketDraft?)null);

        ErrorOr<global::Basket.Application.Basket.Features.AddItemToBasket.V1.AddItemToBasketResponse> result = await handler.Handle(query, CancellationToken.None);

        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.NotFound);
        result.FirstError.Code.ShouldBe("Basket.NotFound");
    }
}

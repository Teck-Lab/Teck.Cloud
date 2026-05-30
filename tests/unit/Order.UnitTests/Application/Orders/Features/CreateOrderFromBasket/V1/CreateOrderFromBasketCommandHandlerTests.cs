using ErrorOr;
using NSubstitute;
using Order.Application.Common.Interfaces;
using Order.Application.Orders.Features.CreateOrderFromBasket.V1;
using Order.Application.Orders.Repositories;
using Order.Domain.Entities.OrderAggregate;
using Shouldly;

namespace Order.UnitTests.Application.Orders.Features.CreateOrderFromBasket.V1;

public sealed class CreateOrderFromBasketCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenBasketClientReturnsNotFound_ShouldReturnNotFoundError()
    {
        IBasketSnapshotClient basketSnapshotClient = Substitute.For<IBasketSnapshotClient>();
        ICatalogValidationClient catalogValidationClient = Substitute.For<ICatalogValidationClient>();
        IOrderRepository orderRepository = Substitute.For<IOrderRepository>();
        CreateOrderFromBasketCommandHandler sut = new(basketSnapshotClient, catalogValidationClient, orderRepository);
        CreateOrderFromBasketCommand command = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        basketSnapshotClient.GetByIdAsync(command.BasketId, command.TenantId, command.CustomerId, Arg.Any<CancellationToken>())
            .Returns(Error.NotFound("Basket.NotFound", "Basket not found"));

        ErrorOr<CreateOrderFromBasketResponse> result = await sut.Handle(command, CancellationToken.None);

        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.NotFound);
        result.FirstError.Code.ShouldBe("Basket.NotFound");
        await orderRepository.DidNotReceive().SaveAsync(Arg.Any<OrderDraft>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenBasketHasNoLines_ShouldReturnBasketEmptyValidationError()
    {
        IBasketSnapshotClient basketSnapshotClient = Substitute.For<IBasketSnapshotClient>();
        ICatalogValidationClient catalogValidationClient = Substitute.For<ICatalogValidationClient>();
        IOrderRepository orderRepository = Substitute.For<IOrderRepository>();
        CreateOrderFromBasketCommandHandler sut = new(basketSnapshotClient, catalogValidationClient, orderRepository);
        CreateOrderFromBasketCommand command = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        BasketSnapshot basket = new(command.BasketId, command.TenantId, command.CustomerId, "USD", []);
        basketSnapshotClient.GetByIdAsync(command.BasketId, command.TenantId, command.CustomerId, Arg.Any<CancellationToken>())
            .Returns(basket);

        ErrorOr<CreateOrderFromBasketResponse> result = await sut.Handle(command, CancellationToken.None);

        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.Validation);
        result.FirstError.Code.ShouldBe("Order.Basket.Empty");
        await orderRepository.DidNotReceive().SaveAsync(Arg.Any<OrderDraft>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenCatalogValidationFailsForLine_ShouldReturnValidationError()
    {
        IBasketSnapshotClient basketSnapshotClient = Substitute.For<IBasketSnapshotClient>();
        ICatalogValidationClient catalogValidationClient = Substitute.For<ICatalogValidationClient>();
        IOrderRepository orderRepository = Substitute.For<IOrderRepository>();
        CreateOrderFromBasketCommandHandler sut = new(basketSnapshotClient, catalogValidationClient, orderRepository);
        CreateOrderFromBasketCommand command = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        Guid productId = Guid.NewGuid();

        BasketSnapshot basket = new(
            command.BasketId,
            command.TenantId,
            command.CustomerId,
            "USD",
            [new BasketSnapshotLine(productId, 2, 10m, "USD")]);

        basketSnapshotClient.GetByIdAsync(command.BasketId, command.TenantId, command.CustomerId, Arg.Any<CancellationToken>())
            .Returns(basket);

        catalogValidationClient.ValidateAsync(Arg.Any<IReadOnlyCollection<CatalogValidationItemRequest>>(), Arg.Any<CancellationToken>())
            .Returns(new CatalogValidationResult([new CatalogValidationItemResult(productId, false, null, null, "price_unavailable")]));

        ErrorOr<CreateOrderFromBasketResponse> result = await sut.Handle(command, CancellationToken.None);

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("Order.CatalogValidation.price_unavailable");
        await orderRepository.DidNotReceive().SaveAsync(Arg.Any<OrderDraft>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenValidBasketAndCatalogValidation_ShouldSaveAndReturnResponse()
    {
        IBasketSnapshotClient basketSnapshotClient = Substitute.For<IBasketSnapshotClient>();
        ICatalogValidationClient catalogValidationClient = Substitute.For<ICatalogValidationClient>();
        IOrderRepository orderRepository = Substitute.For<IOrderRepository>();
        CreateOrderFromBasketCommandHandler sut = new(basketSnapshotClient, catalogValidationClient, orderRepository);
        CreateOrderFromBasketCommand command = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        Guid productOne = Guid.NewGuid();
        Guid productTwo = Guid.NewGuid();

        BasketSnapshot basket = new(
            command.BasketId,
            command.TenantId,
            command.CustomerId,
            "USD",
            [
                new BasketSnapshotLine(productOne, 2, 10m, "USD"),
                new BasketSnapshotLine(productTwo, 1, 5m, "USD"),
            ]);

        basketSnapshotClient.GetByIdAsync(command.BasketId, command.TenantId, command.CustomerId, Arg.Any<CancellationToken>())
            .Returns(basket);

        catalogValidationClient.ValidateAsync(Arg.Any<IReadOnlyCollection<CatalogValidationItemRequest>>(), Arg.Any<CancellationToken>())
            .Returns(new CatalogValidationResult(
            [
                new CatalogValidationItemResult(productOne, true, 12m, "USD", null),
                new CatalogValidationItemResult(productTwo, true, 5m, "USD", null),
            ]));

        ErrorOr<CreateOrderFromBasketResponse> result = await sut.Handle(command, CancellationToken.None);

        result.IsError.ShouldBeFalse();
        result.Value.BasketId.ShouldBe(command.BasketId);
        result.Value.TenantId.ShouldBe(command.TenantId);
        result.Value.CustomerId.ShouldBe(command.CustomerId);
        result.Value.Status.ShouldBe("Pending");
        result.Value.TotalQuantity.ShouldBe(3);
        result.Value.TotalAmount.ShouldBe(29m);
        await orderRepository.Received(1).SaveAsync(Arg.Any<OrderDraft>(), Arg.Any<CancellationToken>());
    }
}

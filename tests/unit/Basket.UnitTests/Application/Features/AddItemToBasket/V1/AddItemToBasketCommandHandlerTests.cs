using Basket.Application.Basket.Features.AddItemToBasket.V1;
using Basket.Application.Basket.Repositories;
using Basket.Application.Common.Interfaces;
using Basket.Domain.Entities.BasketAggregate;
using ErrorOr;
using NSubstitute;
using Shouldly;

namespace Basket.UnitTests.Application.Features.AddItemToBasket.V1;

public sealed class AddItemToBasketCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenCatalogValidationSucceeds_ShouldSaveAndReturnResponse()
    {
        IBasketRepository basketRepository = Substitute.For<IBasketRepository>();
        ICatalogValidationClient catalogValidationClient = Substitute.For<ICatalogValidationClient>();
        AddItemToBasketCommandHandler handler = new(basketRepository, catalogValidationClient);

        AddItemToBasketCommand command = new(Guid.NewGuid(), Guid.NewGuid(), true, Guid.NewGuid(), 2);
        catalogValidationClient.ValidateAsync(Arg.Any<IReadOnlyCollection<CatalogValidationItemRequest>>(), Arg.Any<CancellationToken>())
            .Returns(new CatalogValidationResult([new CatalogValidationItemResult(command.ProductId, true, 19.99m, "USD", null)]));
        basketRepository.GetByTenantAndCustomerAsync(command.TenantId, command.CustomerId, command.IsSignedIn, Arg.Any<CancellationToken>())
            .Returns((BasketDraft?)null);

        ErrorOr<AddItemToBasketResponse> result = await handler.Handle(command, CancellationToken.None);

        result.IsError.ShouldBeFalse();
        result.Value.TotalQuantity.ShouldBe(2);
        await basketRepository.Received(1).SaveAsync(Arg.Any<BasketDraft>(), command.IsSignedIn, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenCatalogValidationFails_ShouldReturnValidationError()
    {
        IBasketRepository basketRepository = Substitute.For<IBasketRepository>();
        ICatalogValidationClient catalogValidationClient = Substitute.For<ICatalogValidationClient>();
        AddItemToBasketCommandHandler handler = new(basketRepository, catalogValidationClient);
        AddItemToBasketCommand command = new(Guid.NewGuid(), Guid.NewGuid(), true, Guid.NewGuid(), 2);

        catalogValidationClient.ValidateAsync(Arg.Any<IReadOnlyCollection<CatalogValidationItemRequest>>(), Arg.Any<CancellationToken>())
            .Returns(new CatalogValidationResult([new CatalogValidationItemResult(command.ProductId, false, null, null, "price_unavailable")]));

        ErrorOr<AddItemToBasketResponse> result = await handler.Handle(command, CancellationToken.None);

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("Basket.Validation.price_unavailable");
        await basketRepository.DidNotReceive().SaveAsync(Arg.Any<BasketDraft>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenCatalogValidationMissingLine_ShouldReturnUnexpectedError()
    {
        IBasketRepository basketRepository = Substitute.For<IBasketRepository>();
        ICatalogValidationClient catalogValidationClient = Substitute.For<ICatalogValidationClient>();
        AddItemToBasketCommandHandler handler = new(basketRepository, catalogValidationClient);
        AddItemToBasketCommand command = new(Guid.NewGuid(), Guid.NewGuid(), true, Guid.NewGuid(), 2);

        catalogValidationClient.ValidateAsync(Arg.Any<IReadOnlyCollection<CatalogValidationItemRequest>>(), Arg.Any<CancellationToken>())
            .Returns(new CatalogValidationResult([]));

        ErrorOr<AddItemToBasketResponse> result = await handler.Handle(command, CancellationToken.None);

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("Basket.CatalogValidation.MissingLine");
    }

    [Fact]
    public async Task Handle_WhenCancellationRequested_ShouldThrowOperationCanceledException()
    {
        IBasketRepository basketRepository = Substitute.For<IBasketRepository>();
        ICatalogValidationClient catalogValidationClient = Substitute.For<ICatalogValidationClient>();
        AddItemToBasketCommandHandler handler = new(basketRepository, catalogValidationClient);
        AddItemToBasketCommand command = new(Guid.NewGuid(), Guid.NewGuid(), true, Guid.NewGuid(), 1);
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        catalogValidationClient.ValidateAsync(Arg.Any<IReadOnlyCollection<CatalogValidationItemRequest>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<ErrorOr<CatalogValidationResult>>(new OperationCanceledException()));

        await Should.ThrowAsync<OperationCanceledException>(() => handler.Handle(command, cts.Token).AsTask());
    }
}

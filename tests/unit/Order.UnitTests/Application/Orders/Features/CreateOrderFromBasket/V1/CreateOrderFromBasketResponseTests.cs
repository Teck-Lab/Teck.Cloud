using Order.Application.Orders.Features.CreateOrderFromBasket.V1;
using Order.Domain.Entities.OrderAggregate;
using Shouldly;

namespace Order.UnitTests.Application.Orders.Features.CreateOrderFromBasket.V1;

public sealed class CreateOrderFromBasketResponseTests
{
    [Fact]
    public void FromDomain_WhenOrderHasLines_ShouldMapTotalsAndCurrencyFromFirstLine()
    {
        Guid tenantId = Guid.NewGuid();
        Guid customerId = Guid.NewGuid();
        Guid basketId = Guid.NewGuid();
        List<OrderLine> lines =
        [
            new(Guid.NewGuid(), 2, 10m, "USD"),
            new(Guid.NewGuid(), 1, 25m, "USD"),
        ];
        OrderDraft order = OrderDraft.Create(tenantId, customerId, basketId, lines);

        CreateOrderFromBasketResponse response = CreateOrderFromBasketResponse.FromDomain(order);

        response.OrderId.ShouldBe(order.Id);
        response.TenantId.ShouldBe(tenantId);
        response.CustomerId.ShouldBe(customerId);
        response.BasketId.ShouldBe(basketId);
        response.Status.ShouldBe("Pending");
        response.TotalQuantity.ShouldBe(3);
        response.TotalAmount.ShouldBe(45m);
        response.CurrencyCode.ShouldBe("USD");
        response.Lines.Count.ShouldBe(2);
    }
}

using Order.Domain.Entities.OrderAggregate;
using Shouldly;

namespace Order.UnitTests.Domain.Entities.OrderAggregate;

public sealed class OrderLineTests
{
    [Fact]
    public void Constructor_WhenValuesProvided_ShouldSetPropertiesAndComputeLineTotal()
    {
        Guid productId = Guid.NewGuid();

        OrderLine line = new(productId, 3, 10.5m, "USD");

        line.ProductId.ShouldBe(productId);
        line.Quantity.ShouldBe(3);
        line.UnitPrice.ShouldBe(10.5m);
        line.CurrencyCode.ShouldBe("USD");
        line.LineTotal.ShouldBe(31.5m);
    }
}

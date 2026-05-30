using Order.Domain.Entities.OrderAggregate;
using Shouldly;

namespace Order.UnitTests.Domain.Entities.OrderAggregate;

public sealed class OrderDraftTests
{
    [Fact]
    public void Create_WhenValidInput_ShouldInitializePendingOrderWithLines()
    {
        Guid tenantId = Guid.NewGuid();
        Guid customerId = Guid.NewGuid();
        Guid basketId = Guid.NewGuid();
        List<OrderLine> lines =
        [
            new(Guid.NewGuid(), 2, 12.5m, "USD"),
            new(Guid.NewGuid(), 1, 5m, "USD"),
        ];

        OrderDraft order = OrderDraft.Create(tenantId, customerId, basketId, lines);

        order.TenantId.ShouldBe(tenantId);
        order.CustomerId.ShouldBe(customerId);
        order.BasketId.ShouldBe(basketId);
        order.Status.ShouldBe("Pending");
        order.Lines.Count.ShouldBe(2);
        order.Lines[0].LineTotal.ShouldBe(25m);
        order.Lines[1].LineTotal.ShouldBe(5m);
        order.DomainEvents.ShouldBeEmpty();
        order.CreatedAtUtc.ShouldBeLessThanOrEqualTo(DateTimeOffset.UtcNow);
    }

    [Fact]
    public void Create_WhenNoLinesProvided_ShouldCreatePendingOrderWithoutLines()
    {
        OrderDraft order = OrderDraft.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), []);

        order.Status.ShouldBe("Pending");
        order.Lines.ShouldBeEmpty();
    }
}

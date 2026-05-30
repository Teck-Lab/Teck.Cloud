using Basket.Domain.Entities.BasketAggregate;
using Shouldly;

namespace Basket.UnitTests.Domain.Entities.BasketAggregate;

public sealed class BasketDraftTests
{
    [Fact]
    public void Create_WhenValid_ShouldInitializeBasket()
    {
        Guid tenantId = Guid.NewGuid();
        Guid customerId = Guid.NewGuid();

        BasketDraft basket = BasketDraft.Create(tenantId, customerId);

        basket.TenantId.ShouldBe(tenantId);
        basket.CustomerId.ShouldBe(customerId);
        basket.Lines.ShouldBeEmpty();
    }

    [Fact]
    public void AddOrUpdateLine_WhenNewProduct_ShouldAddLine()
    {
        BasketDraft basket = BasketDraft.Create(Guid.NewGuid(), Guid.NewGuid());
        Guid productId = Guid.NewGuid();

        basket.AddOrUpdateLine(productId, 2, 10.50m, "USD");

        basket.Lines.Count.ShouldBe(1);
        basket.Lines[0].ProductId.ShouldBe(productId);
        basket.Lines[0].Quantity.ShouldBe(2);
        basket.Lines[0].UnitPrice.ShouldBe(10.50m);
    }

    [Fact]
    public void AddOrUpdateLine_WhenExistingProduct_ShouldIncreaseQuantityAndRefreshPrice()
    {
        BasketDraft basket = BasketDraft.Create(Guid.NewGuid(), Guid.NewGuid());
        Guid productId = Guid.NewGuid();
        basket.AddOrUpdateLine(productId, 1, 9.99m, "USD");

        basket.AddOrUpdateLine(productId, 3, 12.00m, "EUR");

        basket.Lines.Count.ShouldBe(1);
        basket.Lines[0].Quantity.ShouldBe(4);
        basket.Lines[0].UnitPrice.ShouldBe(12.00m);
        basket.Lines[0].CurrencyCode.ShouldBe("EUR");
    }

    [Fact]
    public void AddOrUpdateLine_WhenQuantityIsZero_ShouldThrow()
    {
        BasketDraft basket = BasketDraft.Create(Guid.NewGuid(), Guid.NewGuid());

        Should.Throw<ArgumentOutOfRangeException>(() =>
            basket.AddOrUpdateLine(Guid.NewGuid(), 0, 10m, "USD"));
    }

    [Fact]
    public void Rehydrate_WhenLinesProvided_ShouldCloneLineState()
    {
        Guid basketId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();
        Guid customerId = Guid.NewGuid();
        List<BasketLine> lines = [new(Guid.NewGuid(), 2, 15m, "USD")];

        BasketDraft basket = BasketDraft.Rehydrate(basketId, tenantId, customerId, lines);

        basket.Id.ShouldBe(basketId);
        basket.Lines.Count.ShouldBe(1);
        basket.Lines[0].Quantity.ShouldBe(2);
    }
}

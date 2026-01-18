using Catalog.Domain.Entities.PromotionAggregate.Errors;
using Shouldly;

namespace Catalog.UnitTests.Domain.Promotions;

public class PromotionErrorsTests
{
    [Fact]
    public void EmptyName_Should_Have_Correct_Code_And_Description()
    {
        var err = PromotionErrors.EmptyName;
        err.Code.ShouldBe("Promotion.EmptyName");
        err.Description.ShouldContain("name");
    }

    [Fact]
    public void InvalidDiscount_Should_Have_Correct_Code_And_Description()
    {
        var err = PromotionErrors.InvalidDiscount;
        err.Code.ShouldBe("Promotion.InvalidDiscount");
        err.Description.ShouldContain("greater than zero");
    }

    [Fact]
    public void InvalidDateRange_Should_Have_Correct_Code_And_Description()
    {
        var err = PromotionErrors.InvalidDateRange;
        err.Code.ShouldBe("Promotion.InvalidDateRange");
        err.Description.ShouldContain("date range");
    }

    [Fact]
    public void NoProducts_Should_Have_Correct_Code_And_Description()
    {
        var err = PromotionErrors.NoProducts;
        err.Code.ShouldBe("Promotion.NoProducts");
        err.Description.ShouldBe("Promotion must have at least one product.");
    }
}

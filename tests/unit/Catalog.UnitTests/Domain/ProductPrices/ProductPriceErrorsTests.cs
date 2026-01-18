using Catalog.Domain.Entities.ProductAggregate.Errors;
using Shouldly;

namespace Catalog.UnitTests.Domain.ProductPrices;

public class ProductPriceErrorsTests
{
    [Fact]
    public void NegativePrice_Should_Have_Correct_Code_And_Description()
    {
        var err = ProductPriceErrors.NegativePrice;
        err.Code.ShouldBe("ProductPrice.NegativePrice");
        err.Description.ShouldContain("negative");
    }

    [Fact]
    public void EmptyCurrencyCode_Should_Have_Correct_Code_And_Description()
    {
        var err = ProductPriceErrors.EmptyCurrencyCode;
        err.Code.ShouldBe("ProductPrice.EmptyCurrencyCode");
        err.Description.ShouldContain("currency code");
    }

    [Fact]
    public void DefaultProductId_Should_Have_Correct_Code_And_Description()
    {
        var err = ProductPriceErrors.DefaultProductId;
        err.Code.ShouldBe("ProductPrice.DefaultProductId");
        err.Description.ShouldContain("ProductId");
    }

    [Fact]
    public void DefaultProductPriceTypeId_Should_Have_Correct_Code_And_Description()
    {
        var err = ProductPriceErrors.DefaultProductPriceTypeId;
        err.Code.ShouldBe("ProductPrice.DefaultProductPriceTypeId");
        err.Description.ShouldContain("ProductPriceTypeId");
    }

    [Fact]
    public void NotFound_Should_Have_Correct_Code_And_Description()
    {
        var err = ProductPriceErrors.NotFound;
        err.Code.ShouldBe("ProductPrice.NotFound");
        err.Description.ShouldBe("The specified product price was not found");
    }

    [Fact]
    public void NotCreated_Should_Have_Correct_Code_And_Description()
    {
        var err = ProductPriceErrors.NotCreated;
        err.Code.ShouldBe("ProductPrice.NotCreated");
        err.Description.ShouldBe("The product price was not created");
    }
}

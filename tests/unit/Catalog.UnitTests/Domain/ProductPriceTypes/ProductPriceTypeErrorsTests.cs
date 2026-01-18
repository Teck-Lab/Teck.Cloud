using Catalog.Domain.Entities.ProductPriceTypeAggregate.Errors;
using Shouldly;

namespace Catalog.UnitTests.Domain.ProductPriceTypes;

public class ProductPriceTypeErrorsTests
{
    [Fact]
    public void NotFound_Should_Have_Correct_Code_And_Description()
    {
        var err = ProductPriceTypeErrors.NotFound;
        err.Code.ShouldBe("ProductPriceType.NotFound");
        err.Description.ShouldContain("not found");
    }

    [Fact]
    public void NotCreated_Should_Have_Correct_Code_And_Description()
    {
        var err = ProductPriceTypeErrors.NotCreated;
        err.Code.ShouldBe("ProductPriceType.NotCreated");
        err.Description.ShouldContain("not created");
    }

    [Fact]
    public void EmptyName_Should_Have_Correct_Code_And_Description()
    {
        var err = ProductPriceTypeErrors.EmptyName;
        err.Code.ShouldBe("ProductPriceType.EmptyName");
        err.Description.ShouldContain("name");
    }

    [Fact]
    public void NegativePriority_Should_Have_Correct_Code_And_Description()
    {
        var err = ProductPriceTypeErrors.NegativePriority;
        err.Code.ShouldBe("ProductPriceType.NegativePriority");
        err.Description.ShouldContain("negative");
    }
}

using Catalog.Domain.Entities.ProductAggregate.Errors;
using Shouldly;

namespace Catalog.UnitTests.Domain.Products;

public class ProductErrorsTests
{
    [Fact]
    public void NotFound_Should_Have_Correct_Code_And_Description()
    {
        var err = ProductErrors.NotFound;
        err.Code.ShouldBe("Product.NotFound");
        err.Description.ShouldContain("not found");
    }

    [Fact]
    public void NotCreated_Should_Have_Correct_Code_And_Description()
    {
        var err = ProductErrors.NotCreated;
        err.Code.ShouldBe("Product.NotCreated");
        err.Description.ShouldContain("not created");
    }

    [Fact]
    public void EmptyName_Should_Have_Correct_Code_And_Description()
    {
        var err = ProductErrors.EmptyName;
        err.Code.ShouldBe("Product.EmptyName");
        err.Description.ShouldContain("cannot be empty");
    }

    [Fact]
    public void EmptyDescription_Should_Have_Correct_Code_And_Description()
    {
        var err = ProductErrors.EmptyDescription;
        err.Code.ShouldBe("Product.EmptyDescription");
        err.Description.ShouldContain("cannot be empty");
    }

    [Fact]
    public void EmptySKU_Should_Have_Correct_Code_And_Description()
    {
        var err = ProductErrors.EmptySKU;
        err.Code.ShouldBe("Product.EmptySKU");
        err.Description.ShouldContain("cannot be empty");
    }

    [Fact]
    public void EmptyGTIN_Should_Have_Correct_Code_And_Description()
    {
        var err = ProductErrors.EmptyGTIN;
        err.Code.ShouldBe("Product.EmptyGTIN");
        err.Description.ShouldContain("cannot be empty");
    }

    [Fact]
    public void EmptyCategories_Should_Have_Correct_Code_And_Description()
    {
        var err = ProductErrors.EmptyCategories;
        err.Code.ShouldBe("Product.EmptyCategories");
        err.Description.ShouldContain("cannot be empty");
    }

    [Fact]
    public void NullBrand_Should_Have_Correct_Code_And_Description()
    {
        var err = ProductErrors.NullBrand;
        err.Code.ShouldBe("Product.NullBrand");
        err.Description.ShouldContain("cannot be null");
    }

    [Fact]
    public void NoCategories_Should_Have_Correct_Code_And_Description()
    {
        var err = ProductErrors.NoCategories;
        err.Code.ShouldBe("Product.NoCategories");
        err.Description.ShouldContain("at least one category");
    }

    [Fact]
    public void NoBrand_Should_Have_Correct_Code_And_Description()
    {
        var err = ProductErrors.NoBrand;
        err.Code.ShouldBe("Product.NoBrand");
        err.Description.ShouldContain("must have a brand");
    }
}

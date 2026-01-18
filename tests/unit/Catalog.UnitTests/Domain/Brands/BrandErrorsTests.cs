using Catalog.Domain.Entities.BrandAggregate.Errors;
using ErrorOr;
using Shouldly;

namespace Catalog.UnitTests.Domain.Brands;

public class BrandErrorsTests
{
    [Fact]
    public void NotFound_Should_Have_Correct_Code_And_Description()
    {
        var error = BrandErrors.NotFound;
        error.Code.ShouldBe("Brand.NotFound");
        error.Description.ShouldBe("The specified brand was not found");
        error.Type.ShouldBe(ErrorType.NotFound);
    }

    [Fact]
    public void EmptyName_Should_Have_Correct_Code_And_Description()
    {
        var error = BrandErrors.EmptyName;
        error.Code.ShouldBe("Brand.EmptyName");
        error.Description.ShouldBe("Brand name cannot be empty.");
        error.Type.ShouldBe(ErrorType.Validation);
    }

    [Fact]
    public void EmptyDescription_Should_Have_Correct_Code_And_Description()
    {
        var error = BrandErrors.EmptyDescription;
        error.Code.ShouldBe("Brand.EmptyDescription");
        error.Description.ShouldBe("Brand description cannot be empty.");
        error.Type.ShouldBe(ErrorType.Validation);
    }

    [Fact]
    public void InvalidWebsite_Should_Have_Correct_Code_And_Description()
    {
        var error = BrandErrors.InvalidWebsite;
        error.Code.ShouldBe("Brand.InvalidWebsite");
        error.Description.ShouldBe("Brand website must be a valid URL.");
        error.Type.ShouldBe(ErrorType.Validation);
    }

    [Fact]
    public void EmptyWebsite_Should_Have_Correct_Code_And_Description()
    {
        var error = BrandErrors.EmptyWebsite;
        error.Code.ShouldBe("Brand.EmptyWebsite");
        error.Description.ShouldBe("Brand website cannot be empty.");
        error.Type.ShouldBe(ErrorType.Validation);
    }
}

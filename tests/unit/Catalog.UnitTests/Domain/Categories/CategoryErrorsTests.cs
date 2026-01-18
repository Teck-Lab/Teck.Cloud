using Catalog.Domain.Entities.CategoryAggregate.Errors;
using Shouldly;

namespace Catalog.UnitTests.Domain.Categories;

public class CategoryErrorsTests
{
    [Fact]
    public void NotFound_Should_Have_Correct_Code_And_Description()
    {
        var err = CategoryErrors.NotFound;
        err.Code.ShouldBe("Category.NotFound");
        err.Description.ShouldContain("not found");
    }

    [Fact]
    public void EmptyName_Should_Have_Correct_Code_And_Description()
    {
        var err = CategoryErrors.EmptyName;
        err.Code.ShouldBe("Category.EmptyName");
        err.Description.ShouldContain("cannot be empty");
    }

    [Fact]
    public void EmptyDescription_Should_Have_Correct_Code_And_Description()
    {
        var err = CategoryErrors.EmptyDescription;
        err.Code.ShouldBe("Category.EmptyDescription");
        err.Description.ShouldContain("cannot be empty");
    }
}

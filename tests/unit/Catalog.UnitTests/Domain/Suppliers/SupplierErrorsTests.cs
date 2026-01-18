using Catalog.Domain.Entities.SupplierAggregate.Errors;
using Shouldly;

namespace Catalog.UnitTests.Domain.Suppliers;

public class SupplierErrorsTests
{
    [Fact]
    public void EmptyName_Should_Have_Correct_Code_And_Description()
    {
        var err = SupplierErrors.EmptyName;
        err.Code.ShouldBe("Supplier.EmptyName");
        err.Description.ShouldContain("cannot be empty");
    }

    [Fact]
    public void InvalidWebsite_Should_Have_Correct_Code_And_Description()
    {
        var err = SupplierErrors.InvalidWebsite;
        err.Code.ShouldBe("Supplier.InvalidWebsite");
        err.Description.ShouldContain("valid URL");
    }

    [Fact]
    public void EmptyWebsite_Should_Have_Correct_Code_And_Description()
    {
        var err = SupplierErrors.EmptyWebsite;
        err.Code.ShouldBe("Supplier.EmptyWebsite");
        err.Description.ShouldContain("cannot be empty");
    }

    [Fact]
    public void EmptyDescription_Should_Have_Correct_Code_And_Description()
    {
        var err = SupplierErrors.EmptyDescription;
        err.Code.ShouldBe("Supplier.EmptyDescription");
        err.Description.ShouldContain("cannot be empty");
    }
}

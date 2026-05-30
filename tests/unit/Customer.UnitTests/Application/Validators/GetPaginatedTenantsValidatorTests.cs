using Customer.Application.Tenants.Features.GetPaginatedTenants.V1;
using FluentValidation.TestHelper;

namespace Customer.UnitTests.Application.Validators;

public sealed class GetPaginatedTenantsValidatorTests
{
    private readonly GetPaginatedTenantsValidator _validator = new();

    [Fact]
    public void Validate_WhenRequestIsValid_ShouldNotHaveValidationErrors()
    {
        GetPaginatedTenantsRequest request = new()
        {
            Page = 1,
            Size = 10,
        };

        TestValidationResult<GetPaginatedTenantsRequest> result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WhenPageIsInvalid_ShouldHaveValidationError()
    {
        GetPaginatedTenantsRequest request = new() { Page = 0, Size = 10 };

        TestValidationResult<GetPaginatedTenantsRequest> result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Page);
    }

    [Fact]
    public void Validate_WhenSizeIsInvalid_ShouldHaveValidationError()
    {
        GetPaginatedTenantsRequest request = new() { Page = 1, Size = 0 };

        TestValidationResult<GetPaginatedTenantsRequest> result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Size);
    }
}

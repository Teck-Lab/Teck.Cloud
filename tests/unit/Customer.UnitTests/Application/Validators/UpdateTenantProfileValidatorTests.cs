using Customer.Application.Tenants.Features.UpdateTenantProfile.V1;
using FluentValidation.TestHelper;

namespace Customer.UnitTests.Application.Validators;

public sealed class UpdateTenantProfileValidatorTests
{
    private readonly UpdateTenantProfileValidator _validator = new();

    [Fact]
    public void Validate_WhenIdAndNameProvided_ShouldNotHaveValidationErrors()
    {
        UpdateTenantProfileRequest request = new()
        {
            Id = Guid.NewGuid(),
            Name = "Tenant Updated",
            Plan = null,
        };

        TestValidationResult<UpdateTenantProfileRequest> result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WhenIdIsEmpty_ShouldHaveValidationError()
    {
        UpdateTenantProfileRequest request = new() { Id = Guid.Empty, Name = "Tenant Updated" };

        TestValidationResult<UpdateTenantProfileRequest> result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Id);
    }

    [Fact]
    public void Validate_WhenNoProfileFieldsProvided_ShouldHaveValidationError()
    {
        UpdateTenantProfileRequest request = new() { Id = Guid.NewGuid() };

        TestValidationResult<UpdateTenantProfileRequest> result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x)
            .WithErrorMessage("At least one profile field must be provided");
    }
}

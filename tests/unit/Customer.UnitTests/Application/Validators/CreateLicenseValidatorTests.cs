using Customer.Application.Licenses.Features.CreateLicense.V1;
using FluentValidation.TestHelper;

namespace Customer.UnitTests.Application.Validators;

public sealed class CreateLicenseValidatorTests
{
    private readonly CreateLicenseValidator _validator = new();

    [Fact]
    public void Validate_WhenCommandIsValid_ShouldNotHaveValidationErrors()
    {
        CreateLicenseCommand command = new("tenant-1", null, "Business", "pm_1", "TenantDefault");

        TestValidationResult<CreateLicenseCommand> result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WhenTenantIdMissing_ShouldHaveValidationError()
    {
        CreateLicenseCommand command = new(string.Empty, null, "Business", "pm_1", "TenantDefault");

        TestValidationResult<CreateLicenseCommand> result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.TenantId);
    }

    [Fact]
    public void Validate_WhenPlanMissingOrPaymentScopeMissing_ShouldHaveValidationErrors()
    {
        CreateLicenseCommand command = new("tenant-1", null, string.Empty, "pm_1", string.Empty);

        TestValidationResult<CreateLicenseCommand> result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Plan);
        result.ShouldHaveValidationErrorFor(x => x.PaymentScope);
    }
}

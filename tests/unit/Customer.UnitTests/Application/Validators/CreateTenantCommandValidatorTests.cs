using Customer.Application.Tenants.Features.CreateTenant.V1;
using FluentValidation.TestHelper;

namespace Customer.UnitTests.Application.Validators;

public class CreateTenantCommandValidatorTests
{
    private readonly CreateTenantCommandValidator _validator;

    public CreateTenantCommandValidatorTests()
    {
        _validator = new CreateTenantCommandValidator();
    }

    [Fact]
    public void Validate_ShouldNotHaveErrors_WhenValidCommandProvided()
    {
        // Arrange
        var command = CreateCommand(
            "test-tenant",
            "Test Tenant",
            "Enterprise",
            SharedKernel.Core.Pricing.DatabaseStrategy.Dedicated,
            SharedKernel.Core.Pricing.DatabaseProvider.PostgreSQL);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_ShouldHaveError_WhenIdentifierIsEmpty()
    {
        // Arrange
        var command = CreateCommand(
            "",
            "Test Tenant",
            "Enterprise",
            SharedKernel.Core.Pricing.DatabaseStrategy.Dedicated,
            SharedKernel.Core.Pricing.DatabaseProvider.PostgreSQL);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Identifier);
    }

    [Fact]
    public void Validate_ShouldHaveError_WhenIdentifierExceedsMaxLength()
    {
        // Arrange
        var identifier = new string('a', 101); // 101 characters, max is 100
        var command = CreateCommand(
            identifier,
            "Test Tenant",
            "Enterprise",
            SharedKernel.Core.Pricing.DatabaseStrategy.Dedicated,
            SharedKernel.Core.Pricing.DatabaseProvider.PostgreSQL);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Identifier);
    }

    [Fact]
    public void Validate_ShouldHaveError_WhenNameIsEmpty()
    {
        // Arrange
        var command = CreateCommand(
            "test-tenant",
            "",
            "Enterprise",
            SharedKernel.Core.Pricing.DatabaseStrategy.Dedicated,
            SharedKernel.Core.Pricing.DatabaseProvider.PostgreSQL);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Profile.Name);
    }

    [Fact]
    public void Validate_ShouldHaveError_WhenNameExceedsMaxLength()
    {
        // Arrange
        var name = new string('a', 256); // 256 characters, max is 255
        var command = CreateCommand(
            "test-tenant",
            name,
            "Enterprise",
            SharedKernel.Core.Pricing.DatabaseStrategy.Dedicated,
            SharedKernel.Core.Pricing.DatabaseProvider.PostgreSQL);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Profile.Name);
    }

    [Fact]
    public void Validate_ShouldHaveError_WhenPlanIsEmpty()
    {
        // Arrange
        var command = CreateCommand(
            "test-tenant",
            "Test Tenant",
            "",
            SharedKernel.Core.Pricing.DatabaseStrategy.Dedicated,
            SharedKernel.Core.Pricing.DatabaseProvider.PostgreSQL);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Profile.Plan);
    }

    [Fact]
    public void Validate_ShouldNotHaveError_WhenStrategyIsExternal()
    {
        // Arrange
        var command = CreateCommand(
            "test-tenant",
            "Test Tenant",
            "Enterprise",
            SharedKernel.Core.Pricing.DatabaseStrategy.External,
            SharedKernel.Core.Pricing.DatabaseProvider.PostgreSQL);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    private static CreateTenantCommand CreateCommand(
        string identifier,
        string name,
        string plan,
        SharedKernel.Core.Pricing.DatabaseStrategy strategy,
        SharedKernel.Core.Pricing.DatabaseProvider provider)
    {
        return new CreateTenantCommand(
            identifier,
            new TenantProfile
            {
                Name = name,
                Plan = plan,
            },
            new TenantDatabaseSelection
            {
                DatabaseStrategy = strategy,
                DatabaseProvider = provider,
            });
    }
}

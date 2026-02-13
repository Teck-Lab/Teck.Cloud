using Customer.Application.Tenants.Commands.CreateTenant;
using FluentValidation.TestHelper;
using SharedKernel.Core.Database;

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
        var command = new CreateTenantCommand(
            "test-tenant",
            "Test Tenant",
            "Enterprise",
            SharedKernel.Core.Pricing.DatabaseStrategy.Dedicated,
            SharedKernel.Core.Pricing.DatabaseProvider.PostgreSQL,
            null);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_ShouldHaveError_WhenIdentifierIsEmpty()
    {
        // Arrange
        var command = new CreateTenantCommand(
            "",
            "Test Tenant",
            "Enterprise",
            SharedKernel.Core.Pricing.DatabaseStrategy.Dedicated,
            SharedKernel.Core.Pricing.DatabaseProvider.PostgreSQL,
            null);

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
        var command = new CreateTenantCommand(
            identifier,
            "Test Tenant",
            "Enterprise",
            SharedKernel.Core.Pricing.DatabaseStrategy.Dedicated,
            SharedKernel.Core.Pricing.DatabaseProvider.PostgreSQL,
            null);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Identifier);
    }

    [Fact]
    public void Validate_ShouldHaveError_WhenNameIsEmpty()
    {
        // Arrange
        var command = new CreateTenantCommand(
            "test-tenant",
            "",
            "Enterprise",
            SharedKernel.Core.Pricing.DatabaseStrategy.Dedicated,
            SharedKernel.Core.Pricing.DatabaseProvider.PostgreSQL,
            null);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Name);
    }

    [Fact]
    public void Validate_ShouldHaveError_WhenNameExceedsMaxLength()
    {
        // Arrange
        var name = new string('a', 256); // 256 characters, max is 255
        var command = new CreateTenantCommand(
            "test-tenant",
            name,
            "Enterprise",
            SharedKernel.Core.Pricing.DatabaseStrategy.Dedicated,
            SharedKernel.Core.Pricing.DatabaseProvider.PostgreSQL,
            null);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Name);
    }

    [Fact]
    public void Validate_ShouldHaveError_WhenPlanIsEmpty()
    {
        // Arrange
        var command = new CreateTenantCommand(
            "test-tenant",
            "Test Tenant",
            "",
            SharedKernel.Core.Pricing.DatabaseStrategy.Dedicated,
            SharedKernel.Core.Pricing.DatabaseProvider.PostgreSQL,
            null);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Plan);
    }

    [Fact]
    public void Validate_ShouldHaveError_WhenCustomCredentialsNotProvidedForExternal()
    {
        // Arrange
        var command = new CreateTenantCommand(
            "test-tenant",
            "Test Tenant",
            "Enterprise",
            SharedKernel.Core.Pricing.DatabaseStrategy.External,
            SharedKernel.Core.Pricing.DatabaseProvider.PostgreSQL,
            null); // CustomCredentials is null

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.CustomCredentials);
    }

    [Fact]
    public void Validate_ShouldNotHaveError_WhenCustomCredentialsProvidedForExternal()
    {
        // Arrange
        var customCredentials = new DatabaseCredentials
        {
            Admin = new UserCredentials { Username = "admin", Password = "pass" },
            Application = new UserCredentials { Username = "app", Password = "pass" },
            Host = "localhost",
            Port = 5432,
            Database = "testdb",
            Provider = "PostgreSQL"
        };

        var command = new CreateTenantCommand(
            "test-tenant",
            "Test Tenant",
            "Enterprise",
            SharedKernel.Core.Pricing.DatabaseStrategy.External,
            SharedKernel.Core.Pricing.DatabaseProvider.PostgreSQL,
            customCredentials);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(c => c.CustomCredentials);
    }
}

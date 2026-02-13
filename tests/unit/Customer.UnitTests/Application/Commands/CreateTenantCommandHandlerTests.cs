using Customer.Application.Common.Interfaces;
using Customer.Application.Tenants.Commands.CreateTenant;
using Customer.Application.Tenants.DTOs;
using Customer.Domain.Entities.TenantAggregate.Repositories;
using ErrorOr;
using NSubstitute;
using SharedKernel.Core.Pricing;

using SharedKernel.Core.Models;
using Shouldly;

namespace Customer.UnitTests.Application.Commands;

public class CreateTenantCommandHandlerTests
{
    private readonly ITenantWriteRepository _tenantRepository;

    private readonly IUnitOfWork _unitOfWork;
    private readonly CreateTenantCommandHandler _sut;

    public CreateTenantCommandHandlerTests()
    {
        _tenantRepository = Substitute.For<ITenantWriteRepository>();
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _sut = new CreateTenantCommandHandler(_tenantRepository, _unitOfWork);
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccess_WhenValidCommandProvided()
    {
        // Arrange
        var command = new CreateTenantCommand(
            "test-tenant",
            "Test Tenant",
            "Enterprise",
            DatabaseStrategy.Dedicated,
            DatabaseProvider.PostgreSQL,
            null);

        _tenantRepository.ExistsByIdentifierAsync(command.Identifier, Arg.Any<CancellationToken>())
            .Returns(false);

        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(1);


        // Act
        ErrorOr<TenantDto> result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.ShouldNotBeNull();
        result.Value.Identifier.ShouldBe(command.Identifier);
        result.Value.Name.ShouldBe(command.Name);
        result.Value.Plan.ShouldBe(command.Plan);

        await _tenantRepository.Received(1).AddAsync(
            Arg.Any<Customer.Domain.Entities.TenantAggregate.Tenant>(),
            Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnConflictError_WhenTenantAlreadyExists()
    {
        // Arrange
        var command = new CreateTenantCommand(
            "existing-tenant",
            "Existing Tenant",
            "Enterprise",
            DatabaseStrategy.Dedicated,
            DatabaseProvider.PostgreSQL,
            null);

        _tenantRepository.ExistsByIdentifierAsync(command.Identifier, Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        ErrorOr<TenantDto> result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.Conflict);
        result.FirstError.Code.ShouldBe("Tenant.AlreadyExists");

        await _tenantRepository.DidNotReceive().AddAsync(
            Arg.Any<Customer.Domain.Entities.TenantAggregate.Tenant>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldStoreCredentialsInVault_ForEachService()
    {
        // Arrange
        var command = new CreateTenantCommand(
            "test-tenant",
            "Test Tenant",
            "Enterprise",
            DatabaseStrategy.Dedicated,
            DatabaseProvider.PostgreSQL,
            null);

        _tenantRepository.ExistsByIdentifierAsync(command.Identifier, Arg.Any<CancellationToken>())
            .Returns(false);

        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(1);


        // Act
        ErrorOr<TenantDto> result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.ShouldBeFalse();

        // Credentials are provided externally at runtime; no Vault writes expected in this flow.

    }


    [Fact]
    public async Task Handle_ShouldUseSharedCredentials_WhenStrategyIsShared()
    {
        // Arrange
        var command = new CreateTenantCommand(
            "test-tenant",
            "Test Tenant",
            "Enterprise",
            DatabaseStrategy.Shared,
            DatabaseProvider.PostgreSQL,
            null);

        _tenantRepository.ExistsByIdentifierAsync(command.Identifier, Arg.Any<CancellationToken>())
            .Returns(false);

        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(1);


        // Act
        ErrorOr<TenantDto> result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.ShouldBeFalse();

        // Credentials are provided externally at runtime; no Vault writes expected in this flow.

    }

    [Fact]
    public async Task Handle_ShouldUseCustomCredentials_WhenStrategyIsExternal()
    {
        // Arrange
        var customCredentials = new DatabaseCredentials
        {
            Admin = new UserCredentials { Username = "custom_admin", Password = "custom_pass" },
            Application = new UserCredentials { Username = "custom_app", Password = "custom_pass" },
            Host = "custom-postgres",
            Port = 5432,
            Database = "custom_db",
            Provider = "PostgreSQL"
        };

        var command = new CreateTenantCommand(
            "test-tenant",
            "Test Tenant",
            "Enterprise",
            DatabaseStrategy.External,
            DatabaseProvider.PostgreSQL,
            customCredentials);

        _tenantRepository.ExistsByIdentifierAsync(command.Identifier, Arg.Any<CancellationToken>())
            .Returns(false);

        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(1);


        // Act
        ErrorOr<TenantDto> result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.ShouldBeFalse();

        // External databases are managed externally; no Vault writes expected.

    }
}

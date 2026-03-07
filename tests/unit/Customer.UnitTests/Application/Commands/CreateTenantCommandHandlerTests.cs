using Customer.Application.Common.Interfaces;
using Customer.Application.Tenants.Features.CreateTenant.V1;
using Customer.Application.Tenants.Responses;
using Customer.Domain.Entities.TenantAggregate.Repositories;
using ErrorOr;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using SharedKernel.Core.Database;
using SharedKernel.Core.Pricing;
using Shouldly;

namespace Customer.UnitTests.Application.Commands;

public class CreateTenantCommandHandlerTests
{
    private readonly ITenantWriteRepository _tenantRepository;

    private readonly IUnitOfWork _unitOfWork;
    private readonly ITenantIdentityProvisioningService _tenantIdentityProvisioningService;
    private readonly CreateTenantCommandHandler _sut;

    public CreateTenantCommandHandlerTests()
    {
        _tenantRepository = Substitute.For<ITenantWriteRepository>();
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _tenantIdentityProvisioningService = Substitute.For<ITenantIdentityProvisioningService>();
        _tenantIdentityProvisioningService
            .CreateOrganizationAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("org-test-id");
        _sut = new CreateTenantCommandHandler(_tenantRepository, _unitOfWork, _tenantIdentityProvisioningService);
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccess_WhenValidCommandProvided()
    {
        // Arrange
        var command = CreateCommand(
            "test-tenant",
            "Test Tenant",
            "Enterprise",
            DatabaseStrategy.Dedicated,
            DatabaseProvider.PostgreSQL);

        _tenantRepository.ExistsByIdentifierAsync(command.Identifier, Arg.Any<CancellationToken>())
            .Returns(false);

        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(1);


        // Act
        ErrorOr<TenantResponse> result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.ShouldNotBeNull();
        result.Value.Identifier.ShouldBe(command.Identifier);
        result.Value.Name.ShouldBe(command.Profile.Name);
        result.Value.Plan.ShouldBe(command.Profile.Plan);

        await _tenantRepository.Received(1).AddAsync(
            Arg.Any<Customer.Domain.Entities.TenantAggregate.Tenant>(),
            Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnConflictError_WhenTenantAlreadyExists()
    {
        // Arrange
        var command = CreateCommand(
            "existing-tenant",
            "Existing Tenant",
            "Enterprise",
            DatabaseStrategy.Dedicated,
            DatabaseProvider.PostgreSQL);

        _tenantRepository.ExistsByIdentifierAsync(command.Identifier, Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        ErrorOr<TenantResponse> result = await _sut.Handle(command, CancellationToken.None);

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
        var command = CreateCommand(
            "test-tenant",
            "Test Tenant",
            "Enterprise",
            DatabaseStrategy.Dedicated,
            DatabaseProvider.PostgreSQL);

        _tenantRepository.ExistsByIdentifierAsync(command.Identifier, Arg.Any<CancellationToken>())
            .Returns(false);

        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(1);


        // Act
        ErrorOr<TenantResponse> result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.ShouldBeFalse();

        // Credentials are provided externally at runtime; no Vault writes expected in this flow.

    }


    [Fact]
    public async Task Handle_ShouldUseSharedCredentials_WhenStrategyIsShared()
    {
        // Arrange
        var command = CreateCommand(
            "test-tenant",
            "Test Tenant",
            "Enterprise",
            DatabaseStrategy.Shared,
            DatabaseProvider.PostgreSQL);

        _tenantRepository.ExistsByIdentifierAsync(command.Identifier, Arg.Any<CancellationToken>())
            .Returns(false);

        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(1);


        // Act
        ErrorOr<TenantResponse> result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.ShouldBeFalse();

        // Credentials are provided externally at runtime; no Vault writes expected in this flow.

    }

    [Fact]
    public async Task Handle_ShouldSucceed_WhenStrategyIsExternal()
    {
        // Arrange
        var command = CreateCommand(
            "test-tenant",
            "Test Tenant",
            "Enterprise",
            DatabaseStrategy.External,
            DatabaseProvider.PostgreSQL);

        _tenantRepository.ExistsByIdentifierAsync(command.Identifier, Arg.Any<CancellationToken>())
            .Returns(false);

        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(1);


        // Act
        ErrorOr<TenantResponse> result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.ShouldBeFalse();

        // External databases are managed externally; no Vault writes expected.

    }

    [Fact]
    public async Task Handle_ShouldThrowArgumentNullException_WhenCommandIsNull()
    {
        // Act
        async Task Action()
        {
            _ = await _sut.Handle(null!, CancellationToken.None);
        }

        // Assert
        await Should.ThrowAsync<ArgumentNullException>(Action);
    }

    [Fact]
    public async Task Handle_ShouldThrowInvalidOperationException_WhenPersistenceFailsAfterProvisioning()
    {
        // Arrange
        var command = CreateCommand(
            "tenant-persistence-failure",
            "Tenant Persistence Failure",
            "Enterprise",
            DatabaseStrategy.Dedicated,
            DatabaseProvider.PostgreSQL);

        _tenantRepository.ExistsByIdentifierAsync(command.Identifier, Arg.Any<CancellationToken>())
            .Returns(false);

        _tenantRepository
            .AddAsync(Arg.Any<Customer.Domain.Entities.TenantAggregate.Tenant>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        _unitOfWork
            .When(unitOfWork => unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()))
            .Do(_ => throw new DbUpdateException("save failed"));

        _tenantIdentityProvisioningService
            .DeleteOrganizationAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Act
        async Task Action()
        {
            _ = await _sut.Handle(command, CancellationToken.None);
        }

        // Assert
        var ex = await Should.ThrowAsync<InvalidOperationException>(Action);
        ex.Message.ShouldContain("Tenant persistence failed after identity organization provisioning");
        ex.InnerException.ShouldBeOfType<DbUpdateException>();
        await _tenantIdentityProvisioningService.Received(1)
            .DeleteOrganizationAsync("org-test-id", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldThrowInvalidOperationException_WhenRollbackAlsoFails()
    {
        // Arrange
        var command = CreateCommand(
            "tenant-rollback-failure",
            "Tenant Rollback Failure",
            "Enterprise",
            DatabaseStrategy.Dedicated,
            DatabaseProvider.PostgreSQL);

        _tenantRepository.ExistsByIdentifierAsync(command.Identifier, Arg.Any<CancellationToken>())
            .Returns(false);

        _tenantRepository
            .AddAsync(Arg.Any<Customer.Domain.Entities.TenantAggregate.Tenant>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        _unitOfWork
            .When(unitOfWork => unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()))
            .Do(_ => throw new DbUpdateException("save failed"));

        _tenantIdentityProvisioningService
            .When(service => service.DeleteOrganizationAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()))
            .Do(_ => throw new HttpRequestException("delete failed"));

        // Act
        async Task Action()
        {
            _ = await _sut.Handle(command, CancellationToken.None);
        }

        // Assert
        var ex = await Should.ThrowAsync<InvalidOperationException>(Action);
        ex.Message.ShouldContain("identity rollback also failed");
        ex.InnerException.ShouldBeOfType<HttpRequestException>();
        await _tenantIdentityProvisioningService.Received(1)
            .DeleteOrganizationAsync("org-test-id", Arg.Any<CancellationToken>());
    }

    private static CreateTenantCommand CreateCommand(
        string identifier,
        string name,
        string plan,
        DatabaseStrategy strategy,
        DatabaseProvider provider)
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

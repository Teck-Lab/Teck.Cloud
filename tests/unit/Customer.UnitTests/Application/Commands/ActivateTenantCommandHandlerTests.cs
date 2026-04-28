using Customer.Application.Tenants.Features.ActivateTenant.V1;
using Customer.Application.Tenants.Responses;
using Customer.Domain.Entities.TenantAggregate;
using Customer.Domain.Entities.TenantAggregate.Repositories;
using ErrorOr;
using NSubstitute;
using SharedKernel.Core.Database;
using SharedKernel.Core.Pricing;
using Shouldly;

namespace Customer.UnitTests.Application.Commands;

public sealed class ActivateTenantCommandHandlerTests
{
    private readonly ITenantWriteRepository tenantRepository;
    private readonly IUnitOfWork unitOfWork;
    private readonly ActivateTenantCommandHandler sut;

    public ActivateTenantCommandHandlerTests()
    {
        this.tenantRepository = Substitute.For<ITenantWriteRepository>();
        this.unitOfWork = Substitute.For<IUnitOfWork>();
        this.sut = new ActivateTenantCommandHandler(this.tenantRepository, this.unitOfWork);
    }

    [Fact]
    public async Task Handle_ShouldActivateTenant_WhenTenantExists()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        var tenant = CreateTenant("active-test");
        tenant.Deactivate();

        this.tenantRepository.GetByIdAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(tenant);
        this.unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(1);

        // Act
        ErrorOr<TenantResponse> result = await this.sut.Handle(new ActivateTenantCommand(tenantId), TestContext.Current.CancellationToken);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.IsActive.ShouldBeTrue();
        this.tenantRepository.Received(1).Update(tenant);
        await this.unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenTenantDoesNotExist()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        this.tenantRepository.GetByIdAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns((Tenant?)null);

        // Act
        ErrorOr<TenantResponse> result = await this.sut.Handle(new ActivateTenantCommand(tenantId), TestContext.Current.CancellationToken);

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.NotFound);
        result.FirstError.Code.ShouldBe("Tenant.NotFound");
        await this.unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private static Tenant CreateTenant(string identifier)
    {
        var createResult = Tenant.Create(new TenantCreateArgs
        {
            Identifier = identifier,
            Name = "Tenant",
            Plan = "Business",
            Database = new TenantCreateDatabaseSettings
            {
                DatabaseStrategy = DatabaseStrategy.Dedicated,
                DatabaseProvider = DatabaseProvider.PostgreSQL,
            },
        });

        return createResult.Value;
    }
}

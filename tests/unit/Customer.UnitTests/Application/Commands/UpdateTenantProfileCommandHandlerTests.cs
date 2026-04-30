using Customer.Application.Tenants.Features.UpdateTenantProfile.V1;
using Customer.Application.Tenants.Responses;
using Customer.Domain.Entities.TenantAggregate;
using Customer.Domain.Entities.TenantAggregate.Repositories;
using ErrorOr;
using NSubstitute;
using SharedKernel.Core.Database;
using SharedKernel.Core.Pricing;
using Shouldly;

namespace Customer.UnitTests.Application.Commands;

public sealed class UpdateTenantProfileCommandHandlerTests
{
    private readonly ITenantWriteRepository tenantRepository;
    private readonly IUnitOfWork unitOfWork;
    private readonly UpdateTenantProfileCommandHandler sut;

    public UpdateTenantProfileCommandHandlerTests()
    {
        this.tenantRepository = Substitute.For<ITenantWriteRepository>();
        this.unitOfWork = Substitute.For<IUnitOfWork>();
        this.sut = new UpdateTenantProfileCommandHandler(this.tenantRepository, this.unitOfWork);
    }

    [Fact]
    public async Task Handle_ShouldUpdateTenantProfile_WhenRequestIsValid()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        var tenant = CreateTenant("tenant-profile", "Business");

        this.tenantRepository.GetByIdAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(tenant);
        this.unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(1);

        // Act
        ErrorOr<TenantResponse> result = await this.sut
            .Handle(new UpdateTenantProfileCommand(tenantId, "Tenant Updated", "Enterprise"), TestContext.Current.CancellationToken);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.Name.ShouldBe("Tenant Updated");
        result.Value.Plan.ShouldBe("Enterprise");
        this.tenantRepository.Received(1).Update(tenant);
        await this.unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnValidationError_WhenPlanIsDowngraded()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        var tenant = CreateTenant("tenant-profile", "Enterprise");

        this.tenantRepository.GetByIdAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(tenant);

        // Act
        ErrorOr<TenantResponse> result = await this.sut
            .Handle(new UpdateTenantProfileCommand(tenantId, null, "Business"), TestContext.Current.CancellationToken);

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.Validation);
        result.FirstError.Code.ShouldBe("Tenant.Plan.DowngradeNotAllowed");
        await this.unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnValidationError_WhenNoFieldsProvided()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        var tenant = CreateTenant("tenant-profile", "Business");

        this.tenantRepository.GetByIdAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(tenant);

        // Act
        ErrorOr<TenantResponse> result = await this.sut
            .Handle(new UpdateTenantProfileCommand(tenantId, null, null), TestContext.Current.CancellationToken);

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.Validation);
        result.FirstError.Code.ShouldBe("Tenant.Profile");
        await this.unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private static Tenant CreateTenant(string identifier, string plan)
    {
        var createResult = Tenant.Create(new TenantCreateArgs
        {
            Identifier = identifier,
            Name = "Tenant",
            Plan = plan,
            Database = new TenantCreateDatabaseSettings
            {
                DatabaseStrategy = DatabaseStrategy.Dedicated,
                DatabaseProvider = DatabaseProvider.PostgreSQL,
            },
        });

        return createResult.Value;
    }
}

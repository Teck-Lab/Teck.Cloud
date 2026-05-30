using Customer.Application.Tenants.Features.UpgradeTenantPlan.V1;
using Customer.Domain.Entities.TenantAggregate;
using Customer.Domain.Entities.TenantAggregate.Repositories;
using ErrorOr;
using NSubstitute;
using SharedKernel.Events;
using Shouldly;
using Wolverine;

namespace Customer.UnitTests.Application.Commands;

public sealed class UpgradeTenantPlanCommandHandlerTests
{
    private readonly ITenantWriteRepository _tenantRepository;
    private readonly IMessageBus _messageBus;
    private readonly UpgradeTenantPlanCommandHandler _sut;

    public UpgradeTenantPlanCommandHandlerTests()
    {
        _tenantRepository = Substitute.For<ITenantWriteRepository>();
        _messageBus = Substitute.For<IMessageBus>();
        _sut = new UpgradeTenantPlanCommandHandler(_tenantRepository, _messageBus);
    }

    [Fact]
    public async Task Handle_WhenTenantNotFound_ShouldReturnNotFoundError()
    {
        var command = new UpgradeTenantPlanCommand(Guid.NewGuid(), "Premium", "USD");

        _tenantRepository.GetByIdAsync(command.TenantId, Arg.Any<CancellationToken>())
            .Returns((Tenant?)null);

        ErrorOr<Success> result = await _sut.Handle(command, CancellationToken.None);

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("Tenant.NotFound");
        await _messageBus.DidNotReceive().PublishAsync(Arg.Any<object>());
    }

    [Fact]
    public async Task Handle_WhenTargetPlanIsSameAsCurrentPlanIgnoringCase_ShouldReturnValidationError()
    {
        var tenant = CreateTenant(plan: "Premium", defaultPaymentMethodId: "pm_123");
        var command = new UpgradeTenantPlanCommand(tenant.Id, "premium", "USD");

        _tenantRepository.GetByIdAsync(command.TenantId, Arg.Any<CancellationToken>())
            .Returns(tenant);

        ErrorOr<Success> result = await _sut.Handle(command, CancellationToken.None);

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("Tenant.Plan.SamePlan");
        await _messageBus.DidNotReceive().PublishAsync(Arg.Any<object>());
    }

    [Fact]
    public async Task Handle_WhenTargetPlanIsLowerOrEqualTier_ShouldReturnDowngradeValidationError()
    {
        var tenant = CreateTenant(plan: "Business", defaultPaymentMethodId: "pm_123");
        var command = new UpgradeTenantPlanCommand(tenant.Id, "Premium", "USD");

        _tenantRepository.GetByIdAsync(command.TenantId, Arg.Any<CancellationToken>())
            .Returns(tenant);

        ErrorOr<Success> result = await _sut.Handle(command, CancellationToken.None);

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("Tenant.Plan.Downgrade");
        await _messageBus.DidNotReceive().PublishAsync(Arg.Any<object>());
    }

    [Fact]
    public async Task Handle_WhenDefaultPaymentMethodMissing_ShouldReturnValidationError()
    {
        var tenant = CreateTenant(plan: "Shared", defaultPaymentMethodId: " ");
        var command = new UpgradeTenantPlanCommand(tenant.Id, "Premium", "USD");

        _tenantRepository.GetByIdAsync(command.TenantId, Arg.Any<CancellationToken>())
            .Returns(tenant);

        ErrorOr<Success> result = await _sut.Handle(command, CancellationToken.None);

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("Tenant.Plan.NoPaymentMethod");
        await _messageBus.DidNotReceive().PublishAsync(Arg.Any<object>());
    }

    [Fact]
    public async Task Handle_WhenUpgradeIsValid_ShouldPublishIntegrationEventAndReturnSuccess()
    {
        var tenant = CreateTenant(plan: "Shared", defaultPaymentMethodId: "pm_default");
        var command = new UpgradeTenantPlanCommand(tenant.Id, "Premium", "USD");

        _tenantRepository.GetByIdAsync(command.TenantId, Arg.Any<CancellationToken>())
            .Returns(tenant);

        ErrorOr<Success> result = await _sut.Handle(command, CancellationToken.None);

        result.IsError.ShouldBeFalse();
        await _messageBus.Received(1).PublishAsync(
            Arg.Is<TenantPlanUpgradeRequestedIntegrationEvent>(x =>
                x.TenantId == tenant.Id &&
                x.CurrentPlan == "Shared" &&
                x.TargetPlan == "Premium" &&
                x.PaymentMethodId == "pm_default" &&
                x.Currency == "USD"));
    }

    private static Tenant CreateTenant(string plan, string? defaultPaymentMethodId)
    {
        ErrorOr<Tenant> createResult = Tenant.Create(new TenantCreateArgs
        {
            Identifier = "tenant-id",
            Name = "Tenant Name",
            Plan = plan,
            Database = new TenantCreateDatabaseSettings
            {
                DatabaseStrategy = SharedKernel.Core.Pricing.DatabaseStrategy.Shared,
                DatabaseProvider = SharedKernel.Core.Pricing.DatabaseProvider.PostgreSQL,
            },
        });

        Tenant tenant = createResult.Value;
        tenant.SetPaymentMethod(defaultPaymentMethodId);

        return tenant;
    }
}

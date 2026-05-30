using Customer.Application.Common.Interfaces;
using Customer.Application.Tenants.Features.HandlePlanUpgradePaymentSucceeded.V1;
using Customer.Domain.Entities.LicenseAggregate;
using Customer.Domain.Entities.LicenseAggregate.Repositories;
using Customer.Domain.Entities.TenantAggregate;
using Customer.Domain.Entities.TenantAggregate.Repositories;
using ErrorOr;
using NSubstitute;
using SharedKernel.Core.Pricing;
using Shouldly;

namespace Customer.UnitTests.Application.Commands;

public sealed class HandlePlanUpgradePaymentSucceededCommandHandlerTests
{
    private readonly ITenantWriteRepository _tenantRepository;
    private readonly ILicenseWriteRepository _licenseRepository;
    private readonly ILicenseIssuer _licenseIssuer;
    private readonly HandlePlanUpgradePaymentSucceededCommandHandler _sut;

    public HandlePlanUpgradePaymentSucceededCommandHandlerTests()
    {
        _tenantRepository = Substitute.For<ITenantWriteRepository>();
        _licenseRepository = Substitute.For<ILicenseWriteRepository>();
        _licenseIssuer = Substitute.For<ILicenseIssuer>();
        _sut = new HandlePlanUpgradePaymentSucceededCommandHandler(_tenantRepository, _licenseRepository, _licenseIssuer);
    }

    [Fact]
    public async Task Handle_WhenTenantNotFound_ShouldReturnNotFoundError()
    {
        var command = new HandlePlanUpgradePaymentSucceededCommand(Guid.NewGuid(), Guid.NewGuid(), "Premium", "charge_1");

        _tenantRepository.GetByIdAsync(command.TenantId, Arg.Any<CancellationToken>())
            .Returns((Tenant?)null);

        ErrorOr<Success> result = await _sut.Handle(command, CancellationToken.None);

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("Tenant.NotFound");
    }

    [Fact]
    public async Task Handle_WhenTenantHasCurrentLicense_ShouldSupersedeOldLicenseAndAssignNewOne()
    {
        var oldLicenseId = Guid.NewGuid();
        var tenant = CreateTenant(plan: "Shared", defaultPaymentMethodId: "pm_1", currentLicenseId: oldLicenseId);
        var oldLicense = CreateLicense(tenant.Id.ToString("D"), "Shared", "<old/>", locationId: null);
        var command = new HandlePlanUpgradePaymentSucceededCommand(Guid.NewGuid(), tenant.Id, "Premium", "charge_1");

        _tenantRepository.GetByIdAsync(command.TenantId, Arg.Any<CancellationToken>()).Returns(tenant);
        _licenseRepository.GetByIdAsync(oldLicenseId, Arg.Any<CancellationToken>()).Returns(oldLicense);
        _licenseIssuer.IssueLicenseAsync(
                Arg.Any<string>(),
                Arg.Any<string?>(),
                Arg.Any<string>(),
                Arg.Any<TenantPlan>(),
                Arg.Any<string?>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns("<signed/>");

        ErrorOr<Success> result = await _sut.Handle(command, CancellationToken.None);

        result.IsError.ShouldBeFalse();
        oldLicense.Status.ShouldBe(LicenseStatus.Superseded);
        await _licenseRepository.Received(1).UpdateAsync(oldLicense, Arg.Any<CancellationToken>());
        await _licenseRepository.Received(1).AddAsync(Arg.Any<License>(), Arg.Any<CancellationToken>());
        _tenantRepository.Received(2).Update(tenant);
        tenant.Plan.ShouldBe("Premium");
        tenant.CurrentLicenseId.ShouldNotBe(oldLicenseId);
    }

    [Fact]
    public async Task Handle_WhenCurrentLicenseIdSetButOldLicenseMissing_ShouldStillSucceed()
    {
        var oldLicenseId = Guid.NewGuid();
        var tenant = CreateTenant(plan: "Shared", defaultPaymentMethodId: "pm_2", currentLicenseId: oldLicenseId);
        var command = new HandlePlanUpgradePaymentSucceededCommand(Guid.NewGuid(), tenant.Id, "Premium", "charge_2");

        _tenantRepository.GetByIdAsync(command.TenantId, Arg.Any<CancellationToken>()).Returns(tenant);
        _licenseRepository.GetByIdAsync(oldLicenseId, Arg.Any<CancellationToken>()).Returns((License?)null);
        _licenseIssuer.IssueLicenseAsync(
                Arg.Any<string>(),
                Arg.Any<string?>(),
                Arg.Any<string>(),
                Arg.Any<TenantPlan>(),
                Arg.Any<string?>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns("<signed/>");

        ErrorOr<Success> result = await _sut.Handle(command, CancellationToken.None);

        result.IsError.ShouldBeFalse();
        await _licenseRepository.DidNotReceive().UpdateAsync(Arg.Any<License>(), Arg.Any<CancellationToken>());
        await _licenseRepository.Received(1).AddAsync(Arg.Any<License>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenUpgradeSucceeds_ShouldIssueLicenseForTargetPlan()
    {
        var tenant = CreateTenant(plan: "Shared", defaultPaymentMethodId: "pm_3", currentLicenseId: null);
        var command = new HandlePlanUpgradePaymentSucceededCommand(Guid.NewGuid(), tenant.Id, "Premium", "charge_3");

        _tenantRepository.GetByIdAsync(command.TenantId, Arg.Any<CancellationToken>()).Returns(tenant);
        _licenseIssuer.IssueLicenseAsync(
                Arg.Any<string>(),
                Arg.Any<string?>(),
                Arg.Any<string>(),
                Arg.Any<TenantPlan>(),
                Arg.Any<string?>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns("<signed/>");

        ErrorOr<Success> result = await _sut.Handle(command, CancellationToken.None);

        result.IsError.ShouldBeFalse();
        await _licenseIssuer.Received(1).IssueLicenseAsync(
            tenant.Id.ToString("D"),
            null,
            "Premium",
            Arg.Is<TenantPlan>(plan => plan.Name == "Premium"),
            "pm_3",
            PaymentScope.TenantDefault.Name,
            Arg.Any<CancellationToken>());
    }

    private static Tenant CreateTenant(string plan, string? defaultPaymentMethodId, Guid? currentLicenseId)
    {
        ErrorOr<Tenant> createResult = Tenant.Create(new TenantCreateArgs
        {
            Identifier = "tenant-handler-test",
            Name = "Tenant Handler Test",
            Plan = plan,
            Database = new TenantCreateDatabaseSettings
            {
                DatabaseStrategy = DatabaseStrategy.Shared,
                DatabaseProvider = DatabaseProvider.PostgreSQL,
            },
        });

        Tenant tenant = createResult.Value;
        tenant.SetPaymentMethod(defaultPaymentMethodId);

        if (currentLicenseId.HasValue)
        {
            tenant.AssignLicense(currentLicenseId.Value);
        }

        return tenant;
    }

    private static License CreateLicense(string tenantId, string plan, string licenseXml, string? locationId)
    {
        ErrorOr<License> createResult = License.Create(new LicenseCreateArgs
        {
            TenantId = tenantId,
            LocationId = locationId,
            Plan = plan,
            LicenseXml = licenseXml,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(30),
            PaymentMethodId = "pm_old",
            PaymentScope = PaymentScope.TenantDefault.Name,
            OwnershipType = LicenseOwnershipType.TenantProvided,
        });

        return createResult.Value;
    }
}

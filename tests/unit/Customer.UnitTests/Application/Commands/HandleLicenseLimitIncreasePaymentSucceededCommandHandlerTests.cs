using Customer.Application.Common.Interfaces;
using Customer.Application.Licenses.Features.HandleLicenseLimitIncreasePaymentSucceeded.V1;
using Customer.Domain.Entities.LicenseAggregate;
using Customer.Domain.Entities.LicenseAggregate.Repositories;
using Customer.Domain.Entities.TenantAggregate;
using Customer.Domain.Entities.TenantAggregate.Repositories;
using ErrorOr;
using NSubstitute;
using SharedKernel.Core.Pricing;
using Shouldly;

namespace Customer.UnitTests.Application.Commands;

public sealed class HandleLicenseLimitIncreasePaymentSucceededCommandHandlerTests
{
    private readonly ILicenseWriteRepository _licenseRepository;
    private readonly ITenantWriteRepository _tenantRepository;
    private readonly ILicenseIssuer _licenseIssuer;
    private readonly HandleLicenseLimitIncreasePaymentSucceededCommandHandler _sut;

    public HandleLicenseLimitIncreasePaymentSucceededCommandHandlerTests()
    {
        _licenseRepository = Substitute.For<ILicenseWriteRepository>();
        _tenantRepository = Substitute.For<ITenantWriteRepository>();
        _licenseIssuer = Substitute.For<ILicenseIssuer>();
        _sut = new HandleLicenseLimitIncreasePaymentSucceededCommandHandler(_licenseRepository, _tenantRepository, _licenseIssuer);
    }

    [Fact]
    public async Task Handle_WhenLicenseNotFound_ShouldReturnNotFoundError()
    {
        var command = new HandleLicenseLimitIncreasePaymentSucceededCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "MaxUsers", 25, "charge_1");

        _licenseRepository.GetByIdAsync(command.LicenseId, Arg.Any<CancellationToken>())
            .Returns((License?)null);

        ErrorOr<Success> result = await _sut.Handle(command, CancellationToken.None);

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("License.NotFound");
    }

    [Fact]
    public async Task Handle_WhenTenantNotFound_ShouldReturnNotFoundError()
    {
        var oldLicense = CreateLicense(Guid.NewGuid().ToString("D"), "Premium", "<old/>", locationId: null, paymentMethodId: "pm_old");
        var command = new HandleLicenseLimitIncreasePaymentSucceededCommand(Guid.NewGuid(), Guid.NewGuid(), oldLicense.Id, "MaxUsers", 30, "charge_2");

        _licenseRepository.GetByIdAsync(command.LicenseId, Arg.Any<CancellationToken>()).Returns(oldLicense);
        _tenantRepository.GetByIdAsync(command.TenantId, Arg.Any<CancellationToken>()).Returns((Tenant?)null);

        ErrorOr<Success> result = await _sut.Handle(command, CancellationToken.None);

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("Tenant.NotFound");
    }

    [Fact]
    public async Task Handle_WhenTenantLevelLicense_ShouldSupersedeOldLicenseIssueOverrideAndAssignNewLicense()
    {
        var tenant = CreateTenant(defaultPaymentMethodId: "pm_tenant");
        var oldLicense = CreateLicense(tenant.Id.ToString("D"), "Premium", "<old/>", locationId: null, paymentMethodId: null);
        var command = new HandleLicenseLimitIncreasePaymentSucceededCommand(Guid.NewGuid(), tenant.Id, oldLicense.Id, "MaxUsers", 45, "charge_3");

        _licenseRepository.GetByIdAsync(command.LicenseId, Arg.Any<CancellationToken>()).Returns(oldLicense);
        _tenantRepository.GetByIdAsync(command.TenantId, Arg.Any<CancellationToken>()).Returns(tenant);
        _licenseIssuer.IssueLicenseWithOverridesAsync(
                Arg.Any<string>(),
                Arg.Any<string?>(),
                Arg.Any<string>(),
                Arg.Any<TenantPlan>(),
                Arg.Any<IReadOnlyDictionary<string, string>>(),
                Arg.Any<string?>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns("<signed/>");

        ErrorOr<Success> result = await _sut.Handle(command, CancellationToken.None);

        result.IsError.ShouldBeFalse();
        oldLicense.Status.ShouldBe(LicenseStatus.Superseded);
        await _licenseRepository.Received(1).UpdateAsync(oldLicense, Arg.Any<CancellationToken>());
        await _licenseIssuer.Received(1).IssueLicenseWithOverridesAsync(
            oldLicense.TenantId,
            null,
            "Premium",
            Arg.Is<TenantPlan>(x => x.Name == "Premium"),
            Arg.Is<IReadOnlyDictionary<string, string>>(d => d["MaxUsers"] == "45"),
            "pm_tenant",
            oldLicense.PaymentScope,
            Arg.Any<CancellationToken>());
        await _licenseRepository.Received(1).AddAsync(Arg.Any<License>(), Arg.Any<CancellationToken>());
        _tenantRepository.Received(1).Update(tenant);
    }

    [Fact]
    public async Task Handle_WhenLocationLicense_ShouldNotReassignTenantCurrentLicense()
    {
        var tenant = CreateTenant(defaultPaymentMethodId: "pm_tenant");
        var oldLicense = CreateLicense(tenant.Id.ToString("D"), "Premium", "<old/>", locationId: "loc-1", paymentMethodId: "pm_location");
        var command = new HandleLicenseLimitIncreasePaymentSucceededCommand(Guid.NewGuid(), tenant.Id, oldLicense.Id, "MaxProducts", 500, "charge_4");

        _licenseRepository.GetByIdAsync(command.LicenseId, Arg.Any<CancellationToken>()).Returns(oldLicense);
        _tenantRepository.GetByIdAsync(command.TenantId, Arg.Any<CancellationToken>()).Returns(tenant);
        _licenseIssuer.IssueLicenseWithOverridesAsync(
                Arg.Any<string>(),
                Arg.Any<string?>(),
                Arg.Any<string>(),
                Arg.Any<TenantPlan>(),
                Arg.Any<IReadOnlyDictionary<string, string>>(),
                Arg.Any<string?>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns("<signed/>");

        ErrorOr<Success> result = await _sut.Handle(command, CancellationToken.None);

        result.IsError.ShouldBeFalse();
        _tenantRepository.DidNotReceive().Update(Arg.Any<Tenant>());
        await _licenseRepository.Received(1).AddAsync(Arg.Any<License>(), Arg.Any<CancellationToken>());
    }

    private static Tenant CreateTenant(string? defaultPaymentMethodId)
    {
        ErrorOr<Tenant> createResult = Tenant.Create(new TenantCreateArgs
        {
            Identifier = "tenant-limit-test",
            Name = "Tenant Limit Test",
            Plan = "Premium",
            Database = new TenantCreateDatabaseSettings
            {
                DatabaseStrategy = DatabaseStrategy.Shared,
                DatabaseProvider = DatabaseProvider.PostgreSQL,
            },
        });

        Tenant tenant = createResult.Value;
        tenant.SetPaymentMethod(defaultPaymentMethodId);

        return tenant;
    }

    private static License CreateLicense(string tenantId, string plan, string licenseXml, string? locationId, string? paymentMethodId)
    {
        ErrorOr<License> createResult = License.Create(new LicenseCreateArgs
        {
            TenantId = tenantId,
            LocationId = locationId,
            Plan = plan,
            LicenseXml = licenseXml,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(30),
            PaymentMethodId = paymentMethodId,
            PaymentScope = PaymentScope.TenantDefault.Name,
            OwnershipType = LicenseOwnershipType.TenantProvided,
        });

        return createResult.Value;
    }
}

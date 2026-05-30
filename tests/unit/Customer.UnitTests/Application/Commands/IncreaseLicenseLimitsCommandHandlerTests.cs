using Customer.Application.Licenses.Features.IncreaseLicenseLimits.V1;
using Customer.Domain.Entities.LicenseAggregate;
using Customer.Domain.Entities.LicenseAggregate.Repositories;
using Customer.Domain.Entities.TenantAggregate;
using Customer.Domain.Entities.TenantAggregate.Repositories;
using ErrorOr;
using NSubstitute;
using SharedKernel.Core.Pricing;
using SharedKernel.Events;
using Shouldly;
using Wolverine;

namespace Customer.UnitTests.Application.Commands;

public sealed class IncreaseLicenseLimitsCommandHandlerTests
{
    private readonly ITenantWriteRepository _tenantRepository;
    private readonly ILicenseWriteRepository _licenseRepository;
    private readonly IMessageBus _messageBus;
    private readonly IncreaseLicenseLimitsCommandHandler _sut;

    public IncreaseLicenseLimitsCommandHandlerTests()
    {
        _tenantRepository = Substitute.For<ITenantWriteRepository>();
        _licenseRepository = Substitute.For<ILicenseWriteRepository>();
        _messageBus = Substitute.For<IMessageBus>();
        _sut = new IncreaseLicenseLimitsCommandHandler(_tenantRepository, _licenseRepository, _messageBus);
    }

    [Fact]
    public async Task Handle_WhenValidRequest_ShouldPublishPaymentInitiationEvent()
    {
        Tenant tenant = CreateTenant(Guid.NewGuid(), defaultPaymentMethodId: "pm_default");
        License license = CreateLicense(tenant.Id, paymentMethodId: "pm_license");
        IncreaseLicenseLimitsCommand command = new(tenant.Id, license.Id, "MaxDevices", 10, "USD");

        _tenantRepository.GetByIdAsync(command.TenantId, Arg.Any<CancellationToken>()).Returns(tenant);
        _licenseRepository.GetByIdAsync(command.LicenseId, Arg.Any<CancellationToken>()).Returns(license);

        ErrorOr<Success> result = await _sut.Handle(command, CancellationToken.None);

        result.IsError.ShouldBeFalse();
        await _messageBus.Received(1).PublishAsync(
            Arg.Is<LicenseLimitIncreaseRequestedIntegrationEvent>(e =>
                e.TenantId == tenant.Id &&
                e.LicenseId == license.Id &&
                e.FeatureKey == "MaxDevices" &&
                e.NewLimit == 10 &&
                e.CurrentLimit == 0 &&
                e.PaymentMethodId == "pm_license" &&
                e.Currency == "USD"));
    }

    [Fact]
    public async Task Handle_WhenTenantMissing_ShouldReturnNotFound()
    {
        IncreaseLicenseLimitsCommand command = new(Guid.NewGuid(), Guid.NewGuid(), "MaxDevices", 10, "USD");
        _tenantRepository.GetByIdAsync(command.TenantId, Arg.Any<CancellationToken>()).Returns((Tenant?)null);

        ErrorOr<Success> result = await _sut.Handle(command, CancellationToken.None);

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("Tenant.NotFound");
        await _messageBus.DidNotReceive().PublishAsync(Arg.Any<object>());
    }

    [Fact]
    public async Task Handle_WhenLicenseMissing_ShouldReturnNotFound()
    {
        Tenant tenant = CreateTenant(Guid.NewGuid(), "pm_default");
        IncreaseLicenseLimitsCommand command = new(tenant.Id, Guid.NewGuid(), "MaxDevices", 10, "USD");

        _tenantRepository.GetByIdAsync(command.TenantId, Arg.Any<CancellationToken>()).Returns(tenant);
        _licenseRepository.GetByIdAsync(command.LicenseId, Arg.Any<CancellationToken>()).Returns((License?)null);

        ErrorOr<Success> result = await _sut.Handle(command, CancellationToken.None);

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("License.NotFound");
    }

    [Fact]
    public async Task Handle_WhenNewLimitNotGreater_ShouldReturnValidationError()
    {
        Tenant tenant = CreateTenant(Guid.NewGuid(), "pm_default");
        License license = CreateLicense(tenant.Id, "pm_license");
        IncreaseLicenseLimitsCommand command = new(tenant.Id, license.Id, "MaxDevices", 0, "USD");

        _tenantRepository.GetByIdAsync(command.TenantId, Arg.Any<CancellationToken>()).Returns(tenant);
        _licenseRepository.GetByIdAsync(command.LicenseId, Arg.Any<CancellationToken>()).Returns(license);

        ErrorOr<Success> result = await _sut.Handle(command, CancellationToken.None);

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("License.Limit.NoIncrease");
    }

    [Fact]
    public async Task Handle_WhenNoPaymentMethodAvailable_ShouldReturnValidationError()
    {
        Tenant tenant = CreateTenant(Guid.NewGuid(), null);
        License license = CreateLicense(tenant.Id, null);
        IncreaseLicenseLimitsCommand command = new(tenant.Id, license.Id, "MaxDevices", 12, "USD");

        _tenantRepository.GetByIdAsync(command.TenantId, Arg.Any<CancellationToken>()).Returns(tenant);
        _licenseRepository.GetByIdAsync(command.LicenseId, Arg.Any<CancellationToken>()).Returns(license);

        ErrorOr<Success> result = await _sut.Handle(command, CancellationToken.None);

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("License.Limit.NoPaymentMethod");
        await _messageBus.DidNotReceive().PublishAsync(Arg.Any<object>());
    }

    private static Tenant CreateTenant(Guid id, string? defaultPaymentMethodId)
    {
        TenantCreateArgs args = new()
        {
            Identifier = "tenant-identifier",
            Name = "Tenant",
            Plan = "Business",
            Database = new TenantCreateDatabaseSettings
            {
                DatabaseStrategy = DatabaseStrategy.Dedicated,
                DatabaseProvider = DatabaseProvider.PostgreSQL,
            },
        };

        Tenant tenant = Tenant.Create(args).Value;
        typeof(Tenant).GetProperty("Id")!.SetValue(tenant, id);
        tenant.SetDefaultPaymentMethod(defaultPaymentMethodId);
        return tenant;
    }

    private static License CreateLicense(Guid tenantId, string? paymentMethodId)
    {
        LicenseCreateArgs args = new()
        {
            TenantId = tenantId.ToString("D"),
            LocationId = null,
            Plan = "Business",
            LicenseXml = "<signed/>",
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(30),
            PaymentMethodId = paymentMethodId,
            PaymentScope = "TenantDefault",
        };

        return License.Create(args).Value;
    }
}

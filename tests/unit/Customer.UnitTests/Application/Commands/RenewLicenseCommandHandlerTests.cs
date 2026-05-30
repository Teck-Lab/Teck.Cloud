using Customer.Application.Licenses.Features.RenewLicense.V1;
using Customer.Application.Licenses.Responses;
using Customer.Domain.Entities.LicenseAggregate;
using Customer.Domain.Entities.LicenseAggregate.Repositories;
using Customer.Application.Common.Interfaces;
using ErrorOr;
using NSubstitute;
using Shouldly;
using SharedKernel.Core.Pricing;


namespace Customer.UnitTests.Application.Commands;

public sealed class RenewLicenseCommandHandlerTests
{
    private readonly ILicenseWriteRepository _licenseRepository;
    private readonly ILicenseIssuer _licenseIssuer;
    private readonly RenewLicenseCommandHandler _sut;

    public RenewLicenseCommandHandlerTests()
    {
        _licenseRepository = Substitute.For<ILicenseWriteRepository>();
        _licenseIssuer = Substitute.For<ILicenseIssuer>();
        _sut = new RenewLicenseCommandHandler(_licenseRepository, _licenseIssuer);
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccess_WhenValidCommandProvided()
    {
        var license = CreateLicense();
        var command = new RenewLicenseCommand(license.Id, "Shared", DateTimeOffset.UtcNow.AddYears(1));

        _licenseRepository.GetByIdAsync(license.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<License?>(license));

        _licenseIssuer.IssueLicenseAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<TenantPlan>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("<signed/>" );

        ErrorOr<LicenseResponse> result = await _sut.Handle(command, CancellationToken.None);

        result.IsError.ShouldBeFalse();
        result.Value.ShouldNotBeNull();
        result.Value.Id.ShouldBe(license.Id);
        result.Value.ExpiresAt.ShouldBe(command.NewExpiry);

        await _licenseRepository.Received(1).UpdateAsync(Arg.Is<License>(l => l.Id == license.Id), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldCallLicenseIssuerWithNewPlan_WhenNewPlanProvided()
    {
        var license = CreateLicense();
        var command = new RenewLicenseCommand(license.Id, "Enterprise", DateTimeOffset.UtcNow.AddMonths(6));

        _licenseRepository.GetByIdAsync(license.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<License?>(license));

        _licenseIssuer.IssueLicenseAsync(license.TenantId, license.LocationId, command.NewPlan, Arg.Any<TenantPlan>(), license.PaymentMethodId, license.PaymentScope, Arg.Any<CancellationToken>())
            .Returns("<xml/>");

        _ = await _sut.Handle(command, CancellationToken.None);

        await _licenseIssuer.Received(1).IssueLicenseAsync(license.TenantId, license.LocationId, command.NewPlan, Arg.Any<TenantPlan>(), license.PaymentMethodId, license.PaymentScope, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenLicenseDoesNotExist()
    {
        var command = new RenewLicenseCommand(Guid.NewGuid(), "Shared", DateTimeOffset.UtcNow.AddYears(1));

        _licenseRepository.GetByIdAsync(command.LicenseId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<License?>(null));

        ErrorOr<LicenseResponse> result = await _sut.Handle(command, CancellationToken.None);

        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.NotFound);
        result.FirstError.Code.ShouldBe("License.NotFound");

        await _licenseRepository.DidNotReceive().UpdateAsync(Arg.Any<License>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldThrowArgumentNullException_WhenCommandIsNull()
    {
        async Task Action()
        {
            _ = await _sut.Handle(null!, CancellationToken.None);
        }

        await Should.ThrowAsync<ArgumentNullException>(Action);
    }

    [Fact]
    public async Task Handle_ShouldThrowArgumentException_WhenIssuerReturnsEmptyXml()
    {
        var license = CreateLicense();
        var command = new RenewLicenseCommand(license.Id, "Shared", DateTimeOffset.UtcNow.AddYears(1));

        _licenseRepository.GetByIdAsync(license.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<License?>(license));

        _licenseIssuer.IssueLicenseAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<TenantPlan>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(string.Empty);

        async Task Action()
        {
            _ = await _sut.Handle(command, CancellationToken.None);
        }

        await Should.ThrowAsync<ArgumentException>(Action);
    }

    private static License CreateLicense()
    {
        var args = new LicenseCreateArgs
        {
            TenantId = "tenant-1",
            LocationId = "location-1",
            Plan = "Shared",
            LicenseXml = "<initial/>",
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(30),
            PaymentMethodId = "pm-1",
            PaymentScope = "TenantDefault",
        };

        var created = License.Create(args);
        return created.Value;
    }
}

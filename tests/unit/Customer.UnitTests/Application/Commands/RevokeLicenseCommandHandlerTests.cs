using Customer.Application.Licenses.Features.RevokeLicense.V1;
using Customer.Application.Licenses.Responses;
using Customer.Domain.Entities.LicenseAggregate;
using Customer.Domain.Entities.LicenseAggregate.Repositories;
using ErrorOr;
using NSubstitute;
using Shouldly;


namespace Customer.UnitTests.Application.Commands;

public sealed class RevokeLicenseCommandHandlerTests
{
    private readonly ILicenseWriteRepository _licenseRepository;
    private readonly RevokeLicenseCommandHandler _sut;

    public RevokeLicenseCommandHandlerTests()
    {
        _licenseRepository = Substitute.For<ILicenseWriteRepository>();
        _sut = new RevokeLicenseCommandHandler(_licenseRepository);
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccess_WhenLicenseExists()
    {
        var license = CreateLicense();
        var command = new RevokeLicenseCommand(license.Id);

        _licenseRepository.GetByIdAsync(license.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<License?>(license));

        ErrorOr<LicenseResponse> result = await _sut.Handle(command, CancellationToken.None);

        result.IsError.ShouldBeFalse();
        result.Value.Status.ShouldBe(LicenseStatus.Revoked.Name);

        await _licenseRepository.Received(1).UpdateAsync(Arg.Is<License>(l => l.Id == license.Id && l.Status == LicenseStatus.Revoked), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenLicenseDoesNotExist()
    {
        var command = new RevokeLicenseCommand(Guid.NewGuid());
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
    public async Task Handle_ShouldRevokeExpiredLicense_WhenLicenseIsExpired()
    {
        var license = CreateLicense();
        license.Expire();

        var command = new RevokeLicenseCommand(license.Id);

        _licenseRepository.GetByIdAsync(license.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<License?>(license));

        ErrorOr<LicenseResponse> result = await _sut.Handle(command, CancellationToken.None);

        result.IsError.ShouldBeFalse();
        result.Value.Status.ShouldBe(LicenseStatus.Revoked.Name);
        await _licenseRepository.Received(1).UpdateAsync(Arg.Is<License>(l => l.Id == license.Id && l.Status == LicenseStatus.Revoked), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldRevoke_WhenAlreadyRevoked_NoError()
    {
        var license = CreateLicense();
        license.Revoke();

        var command = new RevokeLicenseCommand(license.Id);

        _licenseRepository.GetByIdAsync(license.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<License?>(license));

        ErrorOr<LicenseResponse> result = await _sut.Handle(command, CancellationToken.None);

        result.IsError.ShouldBeFalse();
        result.Value.Status.ShouldBe(LicenseStatus.Revoked.Name);
        await _licenseRepository.Received(1).UpdateAsync(Arg.Is<License>(l => l.Id == license.Id && l.Status == LicenseStatus.Revoked), Arg.Any<CancellationToken>());
    }

    private static License CreateLicense()
    {
        var args = new LicenseCreateArgs
        {
            TenantId = "tenant-1",
            LocationId = "location-1",
            Plan = "Basic",
            LicenseXml = "<initial/>",
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(30),
            PaymentMethodId = "pm-1",
            PaymentScope = "TenantDefault",
        };

        var created = License.Create(args);
        return created.Value;
    }
}

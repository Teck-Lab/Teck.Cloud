using Customer.Application.Licenses.Features.ExpireLicense.V1;
using Customer.Domain.Entities.LicenseAggregate;
using Customer.Domain.Entities.LicenseAggregate.Repositories;
using ErrorOr;
using NSubstitute;
using Shouldly;

namespace Customer.UnitTests.Application.Commands;

public sealed class ExpireLicenseCommandHandlerTests
{
    private readonly ILicenseWriteRepository _licenseRepository;
    private readonly ExpireLicenseCommandHandler _sut;

    public ExpireLicenseCommandHandlerTests()
    {
        _licenseRepository = Substitute.For<ILicenseWriteRepository>();
        _sut = new ExpireLicenseCommandHandler(_licenseRepository);
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccess_WhenValidCommandProvided()
    {
        var license = CreateLicense();
        var command = new ExpireLicenseCommand(license.Id);

        _licenseRepository.GetByIdAsync(license.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<License?>(license));

        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsError.ShouldBeFalse();
        result.Value.ShouldNotBeNull();
        result.Value.Id.ShouldBe(license.Id);
        result.Value.Status.ShouldBe(LicenseStatus.Expired.Name);

        await _licenseRepository.Received(1).UpdateAsync(Arg.Is<License>(l => l.Id == license.Id), Arg.Any<CancellationToken>());
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
    public async Task Handle_ShouldReturnNotFound_WhenLicenseDoesNotExist()
    {
        var id = Guid.NewGuid();
        var command = new ExpireLicenseCommand(id);

        _licenseRepository.GetByIdAsync(id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<License?>(null));

        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.NotFound);

        await _licenseRepository.DidNotReceive().UpdateAsync(Arg.Any<License>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccess_WhenLicenseAlreadyExpired()
    {
        var license = CreateLicense();
        license.Expire();

        var command = new ExpireLicenseCommand(license.Id);

        _licenseRepository.GetByIdAsync(license.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<License?>(license));

        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsError.ShouldBeFalse();
        result.Value.Status.ShouldBe(LicenseStatus.Expired.Name);

        await _licenseRepository.Received(1).UpdateAsync(Arg.Is<License>(l => l.Id == license.Id), Arg.Any<CancellationToken>());
    }

    private static License CreateLicense()
    {
        var args = new LicenseCreateArgs
        {
            TenantId = "tenant-1",
            LocationId = null,
            Plan = "Pro",
            LicenseXml = "<xml />",
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(30),
            PaymentMethodId = null,
            PaymentScope = "TenantDefault",
        };

        ErrorOr<License> created = License.Create(args);
        return created.Value;
    }
}

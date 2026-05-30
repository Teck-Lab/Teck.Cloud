using Customer.Application.Licenses.Features.AssignLicenseLocation.V1;
using Customer.Application.Licenses.Responses;
using Customer.Domain.Entities.LicenseAggregate;
using Customer.Domain.Entities.LicenseAggregate.Repositories;
using ErrorOr;
using NSubstitute;
using Shouldly;

namespace Customer.UnitTests.Application.Commands;

public sealed class AssignLicenseLocationCommandHandlerTests
{
    private readonly ILicenseWriteRepository _licenseRepository;
    private readonly AssignLicenseLocationCommandHandler _sut;

    public AssignLicenseLocationCommandHandlerTests()
    {
        _licenseRepository = Substitute.For<ILicenseWriteRepository>();
        _sut = new AssignLicenseLocationCommandHandler(_licenseRepository);
    }

    [Fact]
    public async Task Handle_WhenLicenseExists_ShouldUpdateLocationAndReturnResponse()
    {
        License license = CreateLicense();
        AssignLicenseLocationCommand command = new(license.Id, "loc-001");

        _licenseRepository.GetByIdAsync(command.LicenseId, Arg.Any<CancellationToken>())
            .Returns(license);

        ErrorOr<LicenseResponse> result = await _sut.Handle(command, CancellationToken.None);

        result.IsError.ShouldBeFalse();
        result.Value.Id.ShouldBe(license.Id);
        result.Value.LocationId.ShouldBe("loc-001");

        await _licenseRepository.Received(1).UpdateAsync(
            Arg.Is<License>(x => x.Id == license.Id && x.LocationId == "loc-001"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenLicenseDoesNotExist_ShouldReturnNotFound()
    {
        AssignLicenseLocationCommand command = new(Guid.NewGuid(), "loc-001");

        _licenseRepository.GetByIdAsync(command.LicenseId, Arg.Any<CancellationToken>())
            .Returns((License?)null);

        ErrorOr<LicenseResponse> result = await _sut.Handle(command, CancellationToken.None);

        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.NotFound);
        result.FirstError.Code.ShouldBe("License.NotFound");

        await _licenseRepository.DidNotReceive().UpdateAsync(Arg.Any<License>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenUpdateFails_ShouldPropagateException()
    {
        License license = CreateLicense();
        AssignLicenseLocationCommand command = new(license.Id, "loc-001");

        _licenseRepository.GetByIdAsync(command.LicenseId, Arg.Any<CancellationToken>())
            .Returns(license);
        _licenseRepository.When(x => x.UpdateAsync(Arg.Any<License>(), Arg.Any<CancellationToken>()))
            .Do(_ => throw new InvalidOperationException("db failure"));

        async Task Action() => _ = await _sut.Handle(command, CancellationToken.None);

        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(Action);
        ex.Message.ShouldBe("db failure");
    }

    private static License CreateLicense()
    {
        LicenseCreateArgs args = new()
        {
            TenantId = "tenant-001",
            LocationId = null,
            Plan = "Business",
            LicenseXml = "<signed/>",
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(30),
            PaymentMethodId = "pm_1",
            PaymentScope = "TenantDefault",
        };

        return License.Create(args).Value;
    }
}

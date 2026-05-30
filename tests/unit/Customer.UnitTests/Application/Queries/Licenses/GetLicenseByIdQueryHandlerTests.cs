using Customer.Application.Licenses.Features.GetLicenseById.V1;
using Customer.Application.Licenses.Responses;
using Customer.Domain.Entities.LicenseAggregate;
using Customer.Domain.Entities.LicenseAggregate.Repositories;
using ErrorOr;
using NSubstitute;
using Shouldly;

namespace Customer.UnitTests.Application.Queries.Licenses;

public sealed class GetLicenseByIdQueryHandlerTests
{
    private readonly ILicenseWriteRepository _licenseRepository;
    private readonly GetLicenseByIdQueryHandler _sut;

    public GetLicenseByIdQueryHandlerTests()
    {
        _licenseRepository = Substitute.For<ILicenseWriteRepository>();
        _sut = new GetLicenseByIdQueryHandler(_licenseRepository);
    }

    [Fact]
    public async Task Handle_ShouldReturnLicense_WhenFound()
    {
        var license = CreateLicense();
        var query = new GetLicenseByIdQuery(license.Id);

        _licenseRepository.GetByIdAsync(license.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<License?>(license));

        ErrorOr<LicenseResponse> result = await _sut.Handle(query, CancellationToken.None);

        result.IsError.ShouldBeFalse();
        result.Value.ShouldNotBeNull();
        result.Value.Id.ShouldBe(license.Id);
        result.Value.TenantId.ShouldBe(license.TenantId);
        result.Value.Plan.ShouldBe(license.Plan);
        result.Value.Status.ShouldBe(license.Status.Name);
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenLicenseMissing()
    {
        var id = Guid.NewGuid();
        var query = new GetLicenseByIdQuery(id);

        _licenseRepository.GetByIdAsync(id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<License?>(null));

        ErrorOr<LicenseResponse> result = await _sut.Handle(query, CancellationToken.None);

        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.NotFound);
        result.FirstError.Code.ShouldBe("License.NotFound");
    }

    [Fact]
    public async Task Handle_ShouldThrowArgumentNullException_WhenQueryIsNull()
    {
        async Task Action()
        {
            _ = await _sut.Handle(null!, CancellationToken.None);
        }

        await Should.ThrowAsync<ArgumentNullException>(Action);
    }

    [Fact]
    public async Task Handle_ShouldPropagateRepositoryExceptions()
    {
        var id = Guid.NewGuid();
        var query = new GetLicenseByIdQuery(id);

        _licenseRepository.GetByIdAsync(id, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<License?>(new InvalidOperationException("db failure")));

        async Task Action()
        {
            _ = await _sut.Handle(query, CancellationToken.None);
        }

        var ex = await Should.ThrowAsync<InvalidOperationException>(Action);
        ex.Message.ShouldBe("db failure");
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

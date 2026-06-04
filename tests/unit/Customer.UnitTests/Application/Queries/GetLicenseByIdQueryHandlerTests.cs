using Customer.Application.Licenses.Features.GetLicenseById.V1;
using Customer.Application.Licenses.Responses;
using Customer.Domain.Entities.LicenseAggregate.Repositories;
using NSubstitute;
using Shouldly;
using Xunit;
using ErrorOr;

namespace Customer.UnitTests.Application.Queries;

public class GetLicenseByIdQueryHandlerTests
{
    private readonly ILicenseWriteRepository _licenseRepository;
    private readonly GetLicenseByIdQueryHandler _sut;

    public GetLicenseByIdQueryHandlerTests()
    {
        _licenseRepository = Substitute.For<ILicenseWriteRepository>();
        _sut = new GetLicenseByIdQueryHandler(_licenseRepository);
    }

    [Fact]
    public async Task Handle_WhenQueryIsNull_ThrowsArgumentNullException()
    {
        async Task Action()
        {
            _ = await _sut.Handle(null!, CancellationToken.None);
        }

        await Should.ThrowAsync<ArgumentNullException>(Action);
    }

    [Fact]
    public async Task Handle_ShouldReturnLicense_WhenFound()
    {
        var license = CreateLicense("tenant-1", null, "Pro");
        _licenseRepository.GetByIdAsync(license.Id, Arg.Any<CancellationToken>()).Returns(license);

        var query = new GetLicenseByIdQuery(license.Id);

        ErrorOr<LicenseResponse> result = await _sut.Handle(query, CancellationToken.None);

        result.IsError.ShouldBeFalse();
        result.Value.Id.ShouldBe(license.Id);
        result.Value.Plan.ShouldBe("Pro");
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenLicenseMissing()
    {
        var id = Guid.NewGuid();
        _licenseRepository.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns((Customer.Domain.Entities.LicenseAggregate.License?)null);

        var query = new GetLicenseByIdQuery(id);

        ErrorOr<LicenseResponse> result = await _sut.Handle(query, CancellationToken.None);

        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_ShouldPropagateRepositoryExceptions()
    {
        var id = Guid.NewGuid();
        _licenseRepository.When(x => x.GetByIdAsync(id, Arg.Any<CancellationToken>())).Do(_ => throw new InvalidOperationException("db fail"));

        var query = new GetLicenseByIdQuery(id);

        async Task Action()
        {
            _ = await _sut.Handle(query, CancellationToken.None);
        }

        var ex = await Should.ThrowAsync<InvalidOperationException>(Action);
        ex.Message.ShouldContain("db fail");
    }

    private static Customer.Domain.Entities.LicenseAggregate.License CreateLicense(string tenantId, string? locationId, string plan)
    {
        var args = new Customer.Domain.Entities.LicenseAggregate.LicenseCreateArgs
        {
            TenantId = tenantId,
            LocationId = locationId,
            Plan = plan,
            LicenseXml = "<license/>",
            ExpiresAt = DateTimeOffset.UtcNow.AddYears(1),
            PaymentMethodId = null,
            PaymentScope = "TenantDefault",
        };

        return Customer.Domain.Entities.LicenseAggregate.License.Create(args).Value;
    }
}

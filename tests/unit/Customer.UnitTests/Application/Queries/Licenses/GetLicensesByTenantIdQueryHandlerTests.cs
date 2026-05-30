using Customer.Application.Licenses.Features.GetLicensesByTenantId.V1;
using Customer.Application.Licenses.Responses;
using Customer.Domain.Entities.LicenseAggregate;
using Customer.Domain.Entities.LicenseAggregate.Repositories;
using ErrorOr;
using NSubstitute;
using Shouldly;

namespace Customer.UnitTests.Application.Queries.Licenses;

public sealed class GetLicensesByTenantIdQueryHandlerTests
{
    private readonly ILicenseWriteRepository _licenseRepository;
    private readonly GetLicensesByTenantIdQueryHandler _sut;

    public GetLicensesByTenantIdQueryHandlerTests()
    {
        _licenseRepository = Substitute.For<ILicenseWriteRepository>();
        _sut = new GetLicensesByTenantIdQueryHandler(_licenseRepository);
    }

    [Fact]
    public async Task Handle_WhenLicensesExist_ShouldMapResponsesFromRepositoryResult()
    {
        GetLicensesByTenantIdQuery query = new("tenant-123");
        IReadOnlyList<License> licenses = [CreateLicense("tenant-123", "Shared"), CreateLicense("tenant-123", "Enterprise")];

        _licenseRepository.GetByTenantIdAsync(query.TenantId, Arg.Any<CancellationToken>())
            .Returns(licenses);

        ErrorOr<IReadOnlyList<LicenseResponse>> result = await _sut.Handle(query, CancellationToken.None);

        result.IsError.ShouldBeFalse();
        result.Value.Count.ShouldBe(2);
        result.Value[0].TenantId.ShouldBe("tenant-123");
        result.Value[0].Plan.ShouldBe("Shared");
        result.Value[1].Plan.ShouldBe("Enterprise");
    }

    [Fact]
    public async Task Handle_WhenNoLicensesExist_ShouldReturnEmptyCollection()
    {
        GetLicensesByTenantIdQuery query = new("tenant-empty");

        _licenseRepository.GetByTenantIdAsync(query.TenantId, Arg.Any<CancellationToken>())
            .Returns([]);

        ErrorOr<IReadOnlyList<LicenseResponse>> result = await _sut.Handle(query, CancellationToken.None);

        result.IsError.ShouldBeFalse();
        result.Value.ShouldBeEmpty();
    }

    [Fact]
    public async Task Handle_WhenRepositoryThrows_ShouldPropagateException()
    {
        GetLicensesByTenantIdQuery query = new("tenant-err");

        _licenseRepository.GetByTenantIdAsync(query.TenantId, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<IReadOnlyList<License>>(new InvalidOperationException("repository failure")));

        async Task Action() => _ = await _sut.Handle(query, CancellationToken.None);

        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(Action);
        ex.Message.ShouldBe("repository failure");
    }

    private static License CreateLicense(string tenantId, string plan)
    {
        LicenseCreateArgs args = new()
        {
            TenantId = tenantId,
            LocationId = "loc-001",
            Plan = plan,
            LicenseXml = "<xml />",
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(30),
            PaymentMethodId = "pm",
            PaymentScope = "TenantDefault",
        };

        return License.Create(args).Value;
    }
}

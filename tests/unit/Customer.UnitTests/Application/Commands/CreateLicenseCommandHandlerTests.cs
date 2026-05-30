using Customer.Application.Licenses.Features.CreateLicense.V1;
using Customer.Application.Licenses.Responses;
using Customer.Domain.Entities.LicenseAggregate;
using Customer.Domain.Entities.LicenseAggregate.Repositories;
using Customer.Domain.Entities.TenantAggregate;
using Customer.Domain.Entities.TenantAggregate.Repositories;
using Customer.Application.Common.Interfaces;
using ErrorOr;
using NSubstitute;
using SharedKernel.Core.Pricing;
using Shouldly;

namespace Customer.UnitTests.Application.Commands;

public sealed class CreateLicenseCommandHandlerTests
{
    private readonly ILicenseWriteRepository _licenseRepository;
    private readonly ITenantWriteRepository _tenantRepository;
    private readonly ILicenseIssuer _licenseIssuer;
    private readonly CreateLicenseCommandHandler _sut;

    public CreateLicenseCommandHandlerTests()
    {
        _licenseRepository = Substitute.For<ILicenseWriteRepository>();
        _tenantRepository = Substitute.For<ITenantWriteRepository>();
        _licenseIssuer = Substitute.For<ILicenseIssuer>();
        _sut = new CreateLicenseCommandHandler(_licenseRepository, _tenantRepository, _licenseIssuer);
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccess_WhenValidCommandProvided()
    {
        var command = CreateCommand(plan: "Enterprise");
        var tenant = CreateTenant(command.TenantId);

        _licenseIssuer.IssueLicenseAsync(
                Arg.Any<string>(),
                Arg.Any<string?>(),
                Arg.Any<string>(),
                Arg.Any<TenantPlan>(),
                Arg.Any<string?>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns("<signed/>");

        _tenantRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(tenant);

        ErrorOr<LicenseResponse> result = await _sut.Handle(command, CancellationToken.None);

        result.IsError.ShouldBeFalse();
        result.Value.ShouldNotBeNull();
        result.Value.TenantId.ShouldBe(command.TenantId);
        result.Value.Plan.ShouldBe(command.Plan);
        result.Value.Status.ShouldBe(LicenseStatus.Active.Name);

        await _licenseRepository.Received(1).AddAsync(Arg.Any<License>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldCreateTrialLicense_WhenPlanIsTrial()
    {
        var command = CreateCommand(plan: "Trial");
        var tenant = CreateTenant(command.TenantId);

        _licenseIssuer.IssueLicenseAsync(
                Arg.Any<string>(),
                Arg.Any<string?>(),
                Arg.Any<string>(),
                Arg.Any<TenantPlan>(),
                Arg.Any<string?>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns("<signed/>");

        _tenantRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(tenant);

        ErrorOr<LicenseResponse> result = await _sut.Handle(command, CancellationToken.None);

        result.IsError.ShouldBeFalse();
        result.Value.Status.ShouldBe(LicenseStatus.Trial.Name);

        await _licenseRepository.Received(1).AddAsync(Arg.Any<License>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldAssignTenantLicense_WhenLocationIdIsNull_AndTenantExists()
    {
        var command = CreateCommand(locationId: null);
        var tenant = CreateTenant(command.TenantId);

        _licenseIssuer.IssueLicenseAsync(
                Arg.Any<string>(),
                Arg.Any<string?>(),
                Arg.Any<string>(),
                Arg.Any<TenantPlan>(),
                Arg.Any<string?>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns("<signed/>");

        _tenantRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(tenant);

        ErrorOr<LicenseResponse> result = await _sut.Handle(command, CancellationToken.None);

        result.IsError.ShouldBeFalse();
        _tenantRepository.Received(1).Update(Arg.Any<Tenant>());
    }

    [Fact]
    public async Task Handle_ShouldNotAssignTenantLicense_WhenLocationIdIsSet()
    {
        var command = CreateCommand(locationId: "loc-1");
        var tenant = CreateTenant(command.TenantId);

        _licenseIssuer.IssueLicenseAsync(
                Arg.Any<string>(),
                Arg.Any<string?>(),
                Arg.Any<string>(),
                Arg.Any<TenantPlan>(),
                Arg.Any<string?>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns("<signed/>");

        _tenantRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(tenant);

        ErrorOr<LicenseResponse> result = await _sut.Handle(command, CancellationToken.None);

        result.IsError.ShouldBeFalse();
        _tenantRepository.DidNotReceive().Update(Arg.Any<Tenant>());
    }

    [Fact]
    public async Task Handle_ShouldPropagateException_WhenIssuerThrows()
    {
        var command = CreateCommand();
        var tenant = CreateTenant(command.TenantId);

        _licenseIssuer.IssueLicenseAsync(
                Arg.Any<string>(),
                Arg.Any<string?>(),
                Arg.Any<string>(),
                Arg.Any<TenantPlan>(),
                Arg.Any<string?>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException<string>(new InvalidOperationException("issuer failure")));

        _tenantRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(tenant);

        async Task Action()
        {
            _ = await _sut.Handle(command, CancellationToken.None);
        }

        var ex = await Should.ThrowAsync<InvalidOperationException>(Action);
        ex.Message.ShouldBe("issuer failure");
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
    public async Task Handle_ShouldThrow_WhenRepositoryAddThrows()
    {
        var command = CreateCommand();
        var tenant = CreateTenant(command.TenantId);

        _licenseIssuer.IssueLicenseAsync(
                Arg.Any<string>(),
                Arg.Any<string?>(),
                Arg.Any<string>(),
                Arg.Any<TenantPlan>(),
                Arg.Any<string?>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns("<signed/>");

        _tenantRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(tenant);

        _licenseRepository
            .When(repo => repo.AddAsync(Arg.Any<License>(), Arg.Any<CancellationToken>()))
            .Do(_ => throw new InvalidOperationException("db error"));

        async Task Action()
        {
            _ = await _sut.Handle(command, CancellationToken.None);
        }

        var ex = await Should.ThrowAsync<InvalidOperationException>(Action);
        ex.Message.ShouldBe("db error");
    }

    private static CreateLicenseCommand CreateCommand(
        string tenantId = "11111111-1111-1111-1111-111111111111",
        string? locationId = null,
        string plan = "Shared",
        string? paymentMethodId = null,
        string paymentScope = "TenantDefault")
    {
        return new CreateLicenseCommand(tenantId, locationId, plan, paymentMethodId, paymentScope);
    }

    private static Tenant CreateTenant(string tenantId)
    {
        var args = new TenantCreateArgs
        {
            Identifier = "test-tenant",
            Name = "Test Tenant",
            Plan = "Shared",
            Database = new TenantCreateDatabaseSettings
            {
                DatabaseStrategy = DatabaseStrategy.Shared,
                DatabaseProvider = DatabaseProvider.PostgreSQL,
            },
        };

        ErrorOr<Tenant> result = Tenant.Create(args);
        Tenant tenant = result.Value;

        typeof(Tenant).GetProperty("Id")!.SetValue(tenant, Guid.Parse(tenantId));

        return tenant;
    }
}

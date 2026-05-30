using System.Net;
using System.Net.Http.Json;
using Customer.Application.Licenses.Features.ActivateLicense.V1;
using Customer.Application.Licenses.Features.AssignLicenseLocation.V1;
using Customer.Application.Licenses.Features.CreateLicense.V1;
using Customer.Application.Licenses.Features.GetLicenseById.V1;
using Customer.Application.Licenses.Features.GetLicensesByTenantId.V1;
using Customer.Application.Licenses.Features.RenewLicense.V1;
using Customer.Application.Licenses.Responses;
using Customer.IntegrationTests.TestSupport;
using ErrorOr;
using Mediator;
using NSubstitute;
using Shouldly;

#pragma warning disable CA2012
namespace Customer.IntegrationTests.Endpoints.Licenses;

public sealed class LicenseEndpointsIntegrationTests
{
    [Fact]
    public async Task CreateLicense_ShouldReturn401_WhenRequestIsUnauthenticated()
    {
        ISender sender = Substitute.For<ISender>();
        await using TestCustomerApiHost host = await TestCustomerApiHost.StartAsync(sender);

        using HttpRequestMessage request = new(HttpMethod.Post, "/customer/v1/admin/Licenses")
        {
            Content = JsonContent.Create(new { tenantId = "tenant-1", plan = "Business", paymentScope = "TenantDefault" }),
        };

        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateLicense_ShouldReturn400_WhenPayloadIsInvalid()
    {
        ISender sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<CreateLicenseCommand>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<ErrorOr<LicenseResponse>>(Error.Validation("License.Invalid", "invalid payload")));

        await using TestCustomerApiHost host = await TestCustomerApiHost.StartAsync(sender);

        using HttpRequestMessage request = new(HttpMethod.Post, "/customer/v1/admin/Licenses")
        {
            Content = JsonContent.Create(new { tenantId = "", plan = "", paymentScope = "" }),
        };

        request.WithAuthenticatedUser().WithTenantIdClaim(Guid.NewGuid()).WithScopes("license:create");

        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ActivateLicense_ShouldReturn200_WhenCommandSucceeds()
    {
        Guid licenseId = Guid.NewGuid();
        LicenseResponse expected = CreateResponse(licenseId);

        ISender sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<ActivateLicenseCommand>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<ErrorOr<LicenseResponse>>(expected));

        await using TestCustomerApiHost host = await TestCustomerApiHost.StartAsync(sender);
        using HttpRequestMessage request = new(HttpMethod.Put, $"/customer/v1/admin/Licenses/{licenseId:D}/activate")
        {
            Content = JsonContent.Create(new { id = licenseId }),
        };
        request.WithAuthenticatedUser().WithTenantIdClaim(Guid.NewGuid()).WithScopes("license:update");

        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RenewLicense_ShouldReturn404_WhenHandlerReturnsNotFound()
    {
        Guid licenseId = Guid.NewGuid();
        ISender sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<RenewLicenseCommand>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<ErrorOr<LicenseResponse>>(Error.NotFound("License.NotFound", "missing")));

        await using TestCustomerApiHost host = await TestCustomerApiHost.StartAsync(sender);
        using HttpRequestMessage request = new(HttpMethod.Put, $"/customer/v1/admin/Licenses/{licenseId:D}/renew")
        {
            Content = JsonContent.Create(new { newPlan = "Business", newExpiry = DateTimeOffset.UtcNow.AddYears(1) }),
        };
        request.WithAuthenticatedUser().WithTenantIdClaim(Guid.NewGuid()).WithScopes("license:update");

        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetLicenseById_ShouldReturn200_WhenHandlerReturnsLicense()
    {
        Guid licenseId = Guid.NewGuid();
        LicenseResponse expected = CreateResponse(licenseId);
        GetLicenseByIdQuery? captured = null;

        ISender sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<GetLicenseByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                captured = callInfo.Arg<GetLicenseByIdQuery>();
                return new ValueTask<ErrorOr<LicenseResponse>>(expected);
            });

        await using TestCustomerApiHost host = await TestCustomerApiHost.StartAsync(sender);
        using HttpRequestMessage request = new(HttpMethod.Get, $"/customer/v1/admin/Licenses/{licenseId:D}");
        request.WithAuthenticatedUser().WithTenantIdClaim(Guid.NewGuid()).WithScopes("license:list");

        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        captured.ShouldNotBeNull();
        captured.LicenseId.ShouldBe(licenseId);
    }

    [Fact]
    public async Task GetLicensesByTenantId_ShouldReturn200_AndDispatchQueryWithTenantId()
    {
        GetLicensesByTenantIdQuery? captured = null;

        ISender sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<GetLicensesByTenantIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                captured = callInfo.Arg<GetLicensesByTenantIdQuery>();
                IReadOnlyList<LicenseResponse> payload = [CreateResponse(Guid.NewGuid())];
                return new ValueTask<ErrorOr<IReadOnlyList<LicenseResponse>>>(ErrorOrFactory.From<IReadOnlyList<LicenseResponse>>(payload));
            });

        await using TestCustomerApiHost host = await TestCustomerApiHost.StartAsync(sender);
        using HttpRequestMessage request = new(HttpMethod.Get, "/customer/v1/admin/Licenses?tenantId=tenant-123");
        request.WithAuthenticatedUser().WithTenantIdClaim(Guid.NewGuid()).WithScopes("license:list");

        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        captured.ShouldNotBeNull();
        captured.TenantId.ShouldBe("tenant-123");
    }

    [Fact]
    public async Task AssignLicenseLocation_ShouldReturn404_WhenHandlerReturnsNotFound()
    {
        Guid licenseId = Guid.NewGuid();

        ISender sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<AssignLicenseLocationCommand>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<ErrorOr<LicenseResponse>>(Error.NotFound("License.NotFound", "missing")));

        await using TestCustomerApiHost host = await TestCustomerApiHost.StartAsync(sender);
        using HttpRequestMessage request = new(HttpMethod.Post, $"/customer/v1/admin/Licenses/{licenseId:D}/Location")
        {
            Content = JsonContent.Create(new { locationId = "loc-1" }),
        };
        request.WithAuthenticatedUser().WithTenantIdClaim(Guid.NewGuid()).WithScopes("license:update");

        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private static LicenseResponse CreateResponse(Guid id)
    {
        return new LicenseResponse
        {
            Id = id,
            TenantId = "tenant-123",
            LocationId = null,
            Plan = "Business",
            Status = "Active",
            ExpiresAt = DateTimeOffset.UtcNow.AddMonths(1),
            GracePeriodEndsAt = DateTimeOffset.UtcNow.AddMonths(1).AddDays(7),
            PaymentMethodId = "pm_1",
            PaymentScope = "TenantDefault",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
    }
}

#pragma warning restore CA2012

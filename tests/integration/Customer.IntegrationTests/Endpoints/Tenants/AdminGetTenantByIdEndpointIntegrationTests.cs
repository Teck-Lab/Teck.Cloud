using System.Net;
using System.Net.Http.Json;
using Customer.Application.Tenants.Features.GetTenantById.V1;
using Customer.Application.Tenants.Responses;
using Customer.IntegrationTests.TestSupport;
using ErrorOr;
using Mediator;
using NSubstitute;
using Shouldly;
#pragma warning disable CA2012

namespace Customer.IntegrationTests.Endpoints.Tenants;

public sealed class AdminGetTenantByIdEndpointIntegrationTests
{
    [Fact]
    public async Task GetTenantById_ShouldReturn401_WhenRequestIsUnauthenticated()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        ISender sender = Substitute.For<ISender>();
        await using TestCustomerApiHost host = await TestCustomerApiHost.StartAsync(sender);

        // Act
        HttpResponseMessage response = await host.Client.GetAsync(
            new Uri($"/customer/v1/admin/Tenants/{tenantId:D}", UriKind.Relative),
            TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetTenantById_ShouldReturn403_WhenAuthenticatedButMissingScopeClaim()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        ISender sender = Substitute.For<ISender>();
        await using TestCustomerApiHost host = await TestCustomerApiHost.StartAsync(sender);

        using HttpRequestMessage request = new(HttpMethod.Get, $"/customer/v1/admin/Tenants/{tenantId:D}");
        request.WithAuthenticatedUser();

        // Act
        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetTenantById_ShouldReturn404_WhenTenantDoesNotExist()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        ISender sender = Substitute.For<ISender>();

        sender
            .Send(Arg.Any<GetTenantByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<ErrorOr<TenantResponse>>(
                Error.NotFound("Tenant.NotFound", $"Tenant with ID '{tenantId}' not found")));

        await using TestCustomerApiHost host = await TestCustomerApiHost.StartAsync(sender);

        using HttpRequestMessage request = new(HttpMethod.Get, $"/customer/v1/admin/Tenants/{tenantId:D}");
        request.WithAuthenticatedUser().WithScopes("tenant:list");

        // Act
        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetTenantById_ShouldReturn200_AndDispatchQueryWithRouteTenantId()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        GetTenantByIdQuery? capturedQuery = null;

        TenantResponse expectedResponse = new()
        {
            Id = tenantId,
            Identifier = "tenant-admin-lookup",
            Name = "Tenant Admin Lookup",
            Plan = "Business",
            DatabaseStrategy = "Dedicated",
            IsActive = true,
        };

        ISender sender = Substitute.For<ISender>();
        sender
            .Send(Arg.Any<GetTenantByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedQuery = callInfo.Arg<GetTenantByIdQuery>();
                return new ValueTask<ErrorOr<TenantResponse>>(expectedResponse);
            });

        await using TestCustomerApiHost host = await TestCustomerApiHost.StartAsync(sender);

        using HttpRequestMessage request = new(HttpMethod.Get, $"/customer/v1/admin/Tenants/{tenantId:D}");
        request.WithAuthenticatedUser().WithScopes("tenant:list");

        // Act
        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        capturedQuery.ShouldNotBeNull();
        capturedQuery.TenantId.ShouldBe(tenantId);

        TenantResponse? responseBody = await response.Content
            .ReadFromJsonAsync<TenantResponse>(cancellationToken: TestContext.Current.CancellationToken);

        responseBody.ShouldNotBeNull();
        responseBody.Id.ShouldBe(expectedResponse.Id);
        responseBody.Identifier.ShouldBe(expectedResponse.Identifier);
        responseBody.Name.ShouldBe(expectedResponse.Name);
        responseBody.Plan.ShouldBe(expectedResponse.Plan);
        responseBody.IsActive.ShouldBeTrue();
    }
}
#pragma warning restore CA2012

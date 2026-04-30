using System.Net;
using System.Net.Http.Json;
using Customer.Application.Tenants.Features.GetCurrentTenantDatabaseInfo.V1;
using Customer.IntegrationTests.TestSupport;
using ErrorOr;
using Mediator;
using NSubstitute;
using Shouldly;
#pragma warning disable CA2012

namespace Customer.IntegrationTests.Endpoints.Tenants;

public sealed class AdminTenantDatabaseInfoEndpointIntegrationTests
{
    [Fact]
    public async Task GetTenantDatabaseInfo_ShouldReturn401_WhenRequestIsUnauthenticated()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        ISender sender = Substitute.For<ISender>();
        await using TestCustomerApiHost host = await TestCustomerApiHost.StartAsync(sender);

        // Act
        HttpResponseMessage response = await host.Client.GetAsync(
            new Uri($"/customer/v1/admin/Tenants/{tenantId:D}/database-info", UriKind.Relative),
            TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetTenantDatabaseInfo_ShouldReturn403_WhenAuthenticatedButMissingScopeClaim()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        ISender sender = Substitute.For<ISender>();
        await using TestCustomerApiHost host = await TestCustomerApiHost.StartAsync(sender);

        using HttpRequestMessage request = new(HttpMethod.Get, $"/customer/v1/admin/Tenants/{tenantId:D}/database-info");
        request.WithAuthenticatedUser();

        // Act
        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetTenantDatabaseInfo_ShouldReturn200_WithMappedResponse_AndDefaultServiceName()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        GetCurrentTenantDatabaseInfoQuery? capturedQuery = null;

        GetCurrentTenantDatabaseInfoResponse expectedResponse = new()
        {
            TenantId = tenantId,
            Identifier = "tenant-admin-alpha",
            DatabaseStrategy = "Dedicated",
            DatabaseProvider = "PostgreSQL",
            HasReadReplicas = true,
            ServiceName = "customer",
        };

        ISender sender = Substitute.For<ISender>();
        sender
            .Send(Arg.Any<GetCurrentTenantDatabaseInfoQuery>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedQuery = callInfo.Arg<GetCurrentTenantDatabaseInfoQuery>();
                return new ValueTask<ErrorOr<GetCurrentTenantDatabaseInfoResponse>>(expectedResponse);
            });

        await using TestCustomerApiHost host = await TestCustomerApiHost.StartAsync(sender);

        using HttpRequestMessage request = new(HttpMethod.Get, $"/customer/v1/admin/Tenants/{tenantId:D}/database-info");
        request.WithAuthenticatedUser().WithScopes("tenant:list");

        // Act
        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        capturedQuery.ShouldNotBeNull();
        capturedQuery.TenantId.ShouldBe(tenantId);
        capturedQuery.ServiceName.ShouldBe("customer");

        GetCurrentTenantDatabaseInfoResponse? responseBody = await response.Content
            .ReadFromJsonAsync<GetCurrentTenantDatabaseInfoResponse>(cancellationToken: TestContext.Current.CancellationToken);

        responseBody.ShouldNotBeNull();
        responseBody.TenantId.ShouldBe(expectedResponse.TenantId);
        responseBody.Identifier.ShouldBe(expectedResponse.Identifier);
        responseBody.DatabaseStrategy.ShouldBe(expectedResponse.DatabaseStrategy);
        responseBody.DatabaseProvider.ShouldBe(expectedResponse.DatabaseProvider);
        responseBody.HasReadReplicas.ShouldBe(expectedResponse.HasReadReplicas);
        responseBody.ServiceName.ShouldBe(expectedResponse.ServiceName);
    }

    [Fact]
    public async Task GetTenantDatabaseInfo_ShouldPassThroughServiceName_FromQueryString()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        GetCurrentTenantDatabaseInfoQuery? capturedQuery = null;

        ISender sender = Substitute.For<ISender>();
        sender
            .Send(Arg.Any<GetCurrentTenantDatabaseInfoQuery>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedQuery = callInfo.Arg<GetCurrentTenantDatabaseInfoQuery>();
                return new ValueTask<ErrorOr<GetCurrentTenantDatabaseInfoResponse>>(new GetCurrentTenantDatabaseInfoResponse
                {
                    TenantId = tenantId,
                    Identifier = "tenant-admin-alpha",
                    DatabaseStrategy = "Dedicated",
                    DatabaseProvider = "PostgreSQL",
                    HasReadReplicas = false,
                    ServiceName = "billing",
                });
            });

        await using TestCustomerApiHost host = await TestCustomerApiHost.StartAsync(sender);

        using HttpRequestMessage request = new(HttpMethod.Get, $"/customer/v1/admin/Tenants/{tenantId:D}/database-info?ServiceName=billing");
        request.WithAuthenticatedUser().WithScopes("tenant:list");

        // Act
        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        capturedQuery.ShouldNotBeNull();
        capturedQuery.TenantId.ShouldBe(tenantId);
        capturedQuery.ServiceName.ShouldBe("billing");
    }
}
#pragma warning restore CA2012
using System.Net;
using System.Net.Http.Json;
using Customer.Application.Tenants.Features.GetPaginatedTenants.V1;
using Customer.IntegrationTests.TestSupport;
using ErrorOr;
using Mediator;
using NSubstitute;
using SharedKernel.Core.Pagination;
using Shouldly;
#pragma warning disable CA2012

namespace Customer.IntegrationTests.Endpoints.Tenants;

public sealed class AdminGetPaginatedTenantsEndpointIntegrationTests
{
    [Fact]
    public async Task GetPaginatedTenants_ShouldReturn401_WhenRequestIsUnauthenticated()
    {
        // Arrange
        ISender sender = Substitute.For<ISender>();
        await using TestCustomerApiHost host = await TestCustomerApiHost.StartAsync(sender);

        // Act
        HttpResponseMessage response = await host.Client.GetAsync(
            new Uri("/customer/v1/admin/Tenants?page=1&size=10", UriKind.Relative),
            TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetPaginatedTenants_ShouldReturn403_WhenAuthenticatedButMissingScopeClaim()
    {
        // Arrange
        ISender sender = Substitute.For<ISender>();
        await using TestCustomerApiHost host = await TestCustomerApiHost.StartAsync(sender);

        using HttpRequestMessage request = new(HttpMethod.Get, "/customer/v1/admin/Tenants?page=1&size=10");
        request.WithAuthenticatedUser();

        // Act
        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetPaginatedTenants_ShouldReturn400_WhenPageOrSizeIsInvalid()
    {
        // Arrange
        ISender sender = Substitute.For<ISender>();
        await using TestCustomerApiHost host = await TestCustomerApiHost.StartAsync(sender);

        using HttpRequestMessage request = new(HttpMethod.Get, "/customer/v1/admin/Tenants?page=0&size=0");
        request.WithAuthenticatedUser().WithScopes("tenant:list");

        // Act
        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await sender.DidNotReceive().Send(Arg.Any<GetPaginatedTenantsQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetPaginatedTenants_ShouldReturn200_AndDispatchQueryWithFilters()
    {
        // Arrange
        GetPaginatedTenantsQuery? capturedQuery = null;

        PagedList<GetPaginatedTenantsResponse> expectedResponse = new(
            items:
            [
                new GetPaginatedTenantsResponse
                {
                    Id = Guid.NewGuid(),
                    Identifier = "tenant-alpha",
                    Name = "Tenant Alpha",
                    Plan = "Business",
                    DatabaseStrategy = "Dedicated",
                    IsActive = true,
                    CreatedAt = DateTimeOffset.UtcNow,
                },
                new GetPaginatedTenantsResponse
                {
                    Id = Guid.NewGuid(),
                    Identifier = "tenant-beta",
                    Name = "Tenant Beta",
                    Plan = "Business",
                    DatabaseStrategy = "Shared",
                    IsActive = true,
                    CreatedAt = DateTimeOffset.UtcNow,
                },
            ],
            totalItems: 7,
            page: 2,
            size: 2);

        ISender sender = Substitute.For<ISender>();
        sender
            .Send(Arg.Any<GetPaginatedTenantsQuery>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedQuery = callInfo.Arg<GetPaginatedTenantsQuery>();
                return new ValueTask<ErrorOr<PagedList<GetPaginatedTenantsResponse>>>(expectedResponse);
            });

        await using TestCustomerApiHost host = await TestCustomerApiHost.StartAsync(sender);

        using HttpRequestMessage request = new(
            HttpMethod.Get,
            "/customer/v1/admin/Tenants?page=2&size=2&keyword=alpha&plan=Business&isActive=true");
        request.WithAuthenticatedUser().WithScopes("tenant:list");

        // Act
        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        capturedQuery.ShouldNotBeNull();
        capturedQuery.Page.ShouldBe(2);
        capturedQuery.Size.ShouldBe(2);
        capturedQuery.Keyword.ShouldBe("alpha");
        capturedQuery.Plan.ShouldBe("Business");
        capturedQuery.IsActive.ShouldBe(true);

        PagedList<GetPaginatedTenantsResponse>? responseBody = await response.Content
            .ReadFromJsonAsync<PagedList<GetPaginatedTenantsResponse>>(cancellationToken: TestContext.Current.CancellationToken);

        responseBody.ShouldNotBeNull();
        responseBody.Page.ShouldBe(2);
        responseBody.Size.ShouldBe(2);
        responseBody.TotalItems.ShouldBe(7);
        responseBody.TotalPages.ShouldBe(4);
        responseBody.Items.Count.ShouldBe(2);
        responseBody.Items[0].Identifier.ShouldBe("tenant-alpha");
        responseBody.Items[1].Identifier.ShouldBe("tenant-beta");
    }

    [Fact]
    public async Task GetPaginatedTenants_ShouldUseDefaultPagination_WhenPageAndSizeAreOmitted()
    {
        // Arrange
        GetPaginatedTenantsQuery? capturedQuery = null;

        PagedList<GetPaginatedTenantsResponse> expectedResponse = new(
            items:
            [
                new GetPaginatedTenantsResponse
                {
                    Id = Guid.NewGuid(),
                    Identifier = "tenant-default-page",
                    Name = "Tenant Default Page",
                    Plan = "Starter",
                    DatabaseStrategy = "Shared",
                    IsActive = true,
                    CreatedAt = DateTimeOffset.UtcNow,
                },
            ],
            totalItems: 1,
            page: 1,
            size: 10);

        ISender sender = Substitute.For<ISender>();
        sender
            .Send(Arg.Any<GetPaginatedTenantsQuery>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedQuery = callInfo.Arg<GetPaginatedTenantsQuery>();
                return new ValueTask<ErrorOr<PagedList<GetPaginatedTenantsResponse>>>(expectedResponse);
            });

        await using TestCustomerApiHost host = await TestCustomerApiHost.StartAsync(sender);

        using HttpRequestMessage request = new(HttpMethod.Get, "/customer/v1/admin/Tenants");
        request.WithAuthenticatedUser().WithScopes("tenant:list");

        // Act
        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        capturedQuery.ShouldNotBeNull();
        capturedQuery.Page.ShouldBe(1);
        capturedQuery.Size.ShouldBe(10);
        capturedQuery.Keyword.ShouldBeNull();
        capturedQuery.Plan.ShouldBeNull();
        capturedQuery.IsActive.ShouldBeNull();

        PagedList<GetPaginatedTenantsResponse>? responseBody = await response.Content
            .ReadFromJsonAsync<PagedList<GetPaginatedTenantsResponse>>(cancellationToken: TestContext.Current.CancellationToken);

        responseBody.ShouldNotBeNull();
        responseBody.Page.ShouldBe(1);
        responseBody.Size.ShouldBe(10);
        responseBody.TotalItems.ShouldBe(1);
        responseBody.TotalPages.ShouldBe(1);
        responseBody.Items.Count.ShouldBe(1);
        responseBody.Items[0].Identifier.ShouldBe("tenant-default-page");
    }

    [Fact]
    public async Task GetPaginatedTenants_ShouldDispatchFalseIsActiveFilter_AndMapEmptyPage()
    {
        // Arrange
        GetPaginatedTenantsQuery? capturedQuery = null;

        PagedList<GetPaginatedTenantsResponse> expectedResponse = new(
            items: [],
            totalItems: 0,
            page: 1,
            size: 5);

        ISender sender = Substitute.For<ISender>();
        sender
            .Send(Arg.Any<GetPaginatedTenantsQuery>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedQuery = callInfo.Arg<GetPaginatedTenantsQuery>();
                return new ValueTask<ErrorOr<PagedList<GetPaginatedTenantsResponse>>>(expectedResponse);
            });

        await using TestCustomerApiHost host = await TestCustomerApiHost.StartAsync(sender);

        using HttpRequestMessage request = new(
            HttpMethod.Get,
            "/customer/v1/admin/Tenants?page=1&size=5&plan=Enterprise&isActive=false");
        request.WithAuthenticatedUser().WithScopes("tenant:list");

        // Act
        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        capturedQuery.ShouldNotBeNull();
        capturedQuery.Page.ShouldBe(1);
        capturedQuery.Size.ShouldBe(5);
        capturedQuery.Keyword.ShouldBeNull();
        capturedQuery.Plan.ShouldBe("Enterprise");
        capturedQuery.IsActive.ShouldBe(false);

        PagedList<GetPaginatedTenantsResponse>? responseBody = await response.Content
            .ReadFromJsonAsync<PagedList<GetPaginatedTenantsResponse>>(cancellationToken: TestContext.Current.CancellationToken);

        responseBody.ShouldNotBeNull();
        responseBody.Page.ShouldBe(1);
        responseBody.Size.ShouldBe(5);
        responseBody.TotalItems.ShouldBe(0);
        responseBody.TotalPages.ShouldBe(0);
        responseBody.Items.Count.ShouldBe(0);
    }

    [Fact]
    public async Task GetPaginatedTenants_ShouldCapSizeToMaxPageSize_WhenRequestedSizeExceedsLimit()
    {
        // Arrange
        GetPaginatedTenantsQuery? capturedQuery = null;

        PagedList<GetPaginatedTenantsResponse> expectedResponse = new(
            items: [],
            totalItems: 0,
            page: 3,
            size: 100);

        ISender sender = Substitute.For<ISender>();
        sender
            .Send(Arg.Any<GetPaginatedTenantsQuery>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedQuery = callInfo.Arg<GetPaginatedTenantsQuery>();
                return new ValueTask<ErrorOr<PagedList<GetPaginatedTenantsResponse>>>(expectedResponse);
            });

        await using TestCustomerApiHost host = await TestCustomerApiHost.StartAsync(sender);

        using HttpRequestMessage request = new(
            HttpMethod.Get,
            "/customer/v1/admin/Tenants?page=3&size=1000");
        request.WithAuthenticatedUser().WithScopes("tenant:list");

        // Act
        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        capturedQuery.ShouldNotBeNull();
        capturedQuery.Page.ShouldBe(3);
        capturedQuery.Size.ShouldBe(100);
        capturedQuery.Keyword.ShouldBeNull();
        capturedQuery.Plan.ShouldBeNull();
        capturedQuery.IsActive.ShouldBeNull();

        PagedList<GetPaginatedTenantsResponse>? responseBody = await response.Content
            .ReadFromJsonAsync<PagedList<GetPaginatedTenantsResponse>>(cancellationToken: TestContext.Current.CancellationToken);

        responseBody.ShouldNotBeNull();
        responseBody.Page.ShouldBe(3);
        responseBody.Size.ShouldBe(100);
        responseBody.TotalItems.ShouldBe(0);
        responseBody.TotalPages.ShouldBe(0);
        responseBody.Items.Count.ShouldBe(0);
    }

    [Fact]
    public async Task GetPaginatedTenants_ShouldReturn400_WhenIsActiveFilterIsNotBoolean()
    {
        // Arrange
        ISender sender = Substitute.For<ISender>();
        await using TestCustomerApiHost host = await TestCustomerApiHost.StartAsync(sender);

        using HttpRequestMessage request = new(
            HttpMethod.Get,
            "/customer/v1/admin/Tenants?page=1&size=10&isActive=invalid");
        request.WithAuthenticatedUser().WithScopes("tenant:list");

        // Act
        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await sender.DidNotReceive().Send(Arg.Any<GetPaginatedTenantsQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetPaginatedTenants_ShouldDispatchEmptyStringFilters_WhenKeywordAndPlanAreBlank()
    {
        // Arrange
        GetPaginatedTenantsQuery? capturedQuery = null;

        PagedList<GetPaginatedTenantsResponse> expectedResponse = new(
            items: [],
            totalItems: 0,
            page: 1,
            size: 10);

        ISender sender = Substitute.For<ISender>();
        sender
            .Send(Arg.Any<GetPaginatedTenantsQuery>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedQuery = callInfo.Arg<GetPaginatedTenantsQuery>();
                return new ValueTask<ErrorOr<PagedList<GetPaginatedTenantsResponse>>>(expectedResponse);
            });

        await using TestCustomerApiHost host = await TestCustomerApiHost.StartAsync(sender);

        using HttpRequestMessage request = new(
            HttpMethod.Get,
            "/customer/v1/admin/Tenants?page=1&size=10&keyword=&plan=");
        request.WithAuthenticatedUser().WithScopes("tenant:list");

        // Act
        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        capturedQuery.ShouldNotBeNull();
        capturedQuery.Page.ShouldBe(1);
        capturedQuery.Size.ShouldBe(10);
        capturedQuery.Keyword.ShouldBe(string.Empty);
        capturedQuery.Plan.ShouldBe(string.Empty);
        capturedQuery.IsActive.ShouldBeNull();

        PagedList<GetPaginatedTenantsResponse>? responseBody = await response.Content
            .ReadFromJsonAsync<PagedList<GetPaginatedTenantsResponse>>(cancellationToken: TestContext.Current.CancellationToken);

        responseBody.ShouldNotBeNull();
        responseBody.Page.ShouldBe(1);
        responseBody.Size.ShouldBe(10);
        responseBody.TotalItems.ShouldBe(0);
        responseBody.TotalPages.ShouldBe(0);
        responseBody.Items.Count.ShouldBe(0);
    }

    [Fact]
    public async Task GetPaginatedTenants_ShouldDispatchWhitespaceFilters_WhenKeywordAndPlanContainSpaces()
    {
        // Arrange
        GetPaginatedTenantsQuery? capturedQuery = null;

        PagedList<GetPaginatedTenantsResponse> expectedResponse = new(
            items: [],
            totalItems: 0,
            page: 1,
            size: 10);

        ISender sender = Substitute.For<ISender>();
        sender
            .Send(Arg.Any<GetPaginatedTenantsQuery>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedQuery = callInfo.Arg<GetPaginatedTenantsQuery>();
                return new ValueTask<ErrorOr<PagedList<GetPaginatedTenantsResponse>>>(expectedResponse);
            });

        await using TestCustomerApiHost host = await TestCustomerApiHost.StartAsync(sender);

        using HttpRequestMessage request = new(
            HttpMethod.Get,
            "/customer/v1/admin/Tenants?page=1&size=10&keyword=%20%20&plan=%20%20");
        request.WithAuthenticatedUser().WithScopes("tenant:list");

        // Act
        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        capturedQuery.ShouldNotBeNull();
        capturedQuery.Page.ShouldBe(1);
        capturedQuery.Size.ShouldBe(10);
        capturedQuery.Keyword.ShouldBe("  ");
        capturedQuery.Plan.ShouldBe("  ");
        capturedQuery.IsActive.ShouldBeNull();

        PagedList<GetPaginatedTenantsResponse>? responseBody = await response.Content
            .ReadFromJsonAsync<PagedList<GetPaginatedTenantsResponse>>(cancellationToken: TestContext.Current.CancellationToken);

        responseBody.ShouldNotBeNull();
        responseBody.Page.ShouldBe(1);
        responseBody.Size.ShouldBe(10);
        responseBody.TotalItems.ShouldBe(0);
        responseBody.TotalPages.ShouldBe(0);
        responseBody.Items.Count.ShouldBe(0);
    }

    [Fact]
    public async Task GetPaginatedTenants_ShouldDispatchMixedEmptyAndWhitespaceFilters_WhenQueryContainsBothShapes()
    {
        // Arrange
        GetPaginatedTenantsQuery? capturedQuery = null;

        PagedList<GetPaginatedTenantsResponse> expectedResponse = new(
            items: [],
            totalItems: 0,
            page: 1,
            size: 10);

        ISender sender = Substitute.For<ISender>();
        sender
            .Send(Arg.Any<GetPaginatedTenantsQuery>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedQuery = callInfo.Arg<GetPaginatedTenantsQuery>();
                return new ValueTask<ErrorOr<PagedList<GetPaginatedTenantsResponse>>>(expectedResponse);
            });

        await using TestCustomerApiHost host = await TestCustomerApiHost.StartAsync(sender);

        using HttpRequestMessage request = new(
            HttpMethod.Get,
            "/customer/v1/admin/Tenants?page=1&size=10&keyword=&plan=%20%20");
        request.WithAuthenticatedUser().WithScopes("tenant:list");

        // Act
        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        capturedQuery.ShouldNotBeNull();
        capturedQuery.Page.ShouldBe(1);
        capturedQuery.Size.ShouldBe(10);
        capturedQuery.Keyword.ShouldBe(string.Empty);
        capturedQuery.Plan.ShouldBe("  ");
        capturedQuery.IsActive.ShouldBeNull();

        PagedList<GetPaginatedTenantsResponse>? responseBody = await response.Content
            .ReadFromJsonAsync<PagedList<GetPaginatedTenantsResponse>>(cancellationToken: TestContext.Current.CancellationToken);

        responseBody.ShouldNotBeNull();
        responseBody.Page.ShouldBe(1);
        responseBody.Size.ShouldBe(10);
        responseBody.TotalItems.ShouldBe(0);
        responseBody.TotalPages.ShouldBe(0);
        responseBody.Items.Count.ShouldBe(0);
    }
}
#pragma warning restore CA2012

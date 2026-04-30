using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Customer.Application.Tenants.Features.GetTenantById.V1;
using Customer.Application.Tenants.Features.UpdateTenantProfile.V1;
using Customer.Application.Tenants.Responses;
using Customer.IntegrationTests.TestSupport;
using ErrorOr;
using Mediator;
using NSubstitute;
using Shouldly;
#pragma warning disable CA2012

namespace Customer.IntegrationTests.Endpoints.Tenants;

public sealed class CurrentTenantProfileEndpointsIntegrationTests
{
    [Fact]
    public async Task GetCurrentTenantProfile_ShouldReturn401_WhenRequestIsUnauthenticated()
    {
        // Arrange
        ISender sender = Substitute.For<ISender>();
        await using TestCustomerApiHost host = await TestCustomerApiHost.StartAsync(sender);

        // Act
        HttpResponseMessage response = await host.Client.GetAsync(
            new Uri("/customer/v1/Tenants/me", UriKind.Relative),
            TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PatchCurrentTenantProfile_ShouldReturn401_WhenRequestIsUnauthenticated()
    {
        // Arrange
        ISender sender = Substitute.For<ISender>();
        await using TestCustomerApiHost host = await TestCustomerApiHost.StartAsync(sender);

        using HttpRequestMessage request = new(HttpMethod.Patch, "/customer/v1/Tenants/me")
        {
            Content = JsonContent.Create(new
            {
                name = "Tenant Renamed",
            }),
        };

        // Act
        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetCurrentTenantProfile_ShouldReturn403_WhenAuthenticatedButMissingScopeClaim()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        ISender sender = Substitute.For<ISender>();
        await using TestCustomerApiHost host = await TestCustomerApiHost.StartAsync(sender);

        using HttpRequestMessage request = new(HttpMethod.Get, "/customer/v1/Tenants/me");
        request.WithAuthenticatedUser().WithTenantIdClaim(tenantId);

        // Act
        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetCurrentTenantProfile_ShouldReturn400_WhenTenantContextIsMissing()
    {
        // Arrange
        ISender sender = Substitute.For<ISender>();
        await using TestCustomerApiHost host = await TestCustomerApiHost.StartAsync(sender);

        using HttpRequestMessage request = new(HttpMethod.Get, "/customer/v1/Tenants/me");
        request.WithAuthenticatedUser().WithScopes("tenant:list");

        // Act
        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        string responseBody = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        ResponseContainsValidationKey(responseBody, "Tenant.Context").ShouldBeTrue();
    }

    [Fact]
    public async Task GetCurrentTenantProfile_ShouldResolveTenantFromClaim_AndReturnMappedResponse()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        GetTenantByIdQuery? capturedQuery = null;

        TenantResponse expectedResponse = new()
        {
            Id = tenantId,
            Identifier = "tenant-alpha",
            Name = "Tenant Alpha",
            Plan = "Business",
            DatabaseStrategy = "Dedicated",
            IsActive = true,
            Databases =
            [
                new TenantDatabaseMetadataResponse
                {
                    ServiceName = "customer",
                    WriteEnvVarKey = "TENANT_ALPHA_CUSTOMER_DB_WRITE",
                    ReadEnvVarKey = "TENANT_ALPHA_CUSTOMER_DB_READ",
                    HasSeparateReadDatabase = true,
                },
            ],
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

        using HttpRequestMessage request = new(HttpMethod.Get, "/customer/v1/Tenants/me");
        request.WithAuthenticatedUser().WithTenantIdClaim(tenantId).WithScopes("tenant:list");

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
        responseBody.DatabaseStrategy.ShouldBe(expectedResponse.DatabaseStrategy);
        responseBody.Databases.Count.ShouldBe(1);
        responseBody.Databases.First().ServiceName.ShouldBe("customer");
    }

    [Fact]
    public async Task PatchCurrentTenantProfile_ShouldResolveTenantFromContextAccessor_AndReturnMappedResponse()
    {
        // Arrange
        Guid tenantIdFromContext = Guid.NewGuid();
        Guid tenantIdFromClaim = Guid.NewGuid();
        UpdateTenantProfileCommand? capturedCommand = null;

        TenantResponse expectedResponse = new()
        {
            Id = tenantIdFromContext,
            Identifier = "tenant-context",
            Name = "Tenant Context Updated",
            Plan = "Enterprise",
            DatabaseStrategy = "Dedicated",
            IsActive = true,
        };

        ISender sender = Substitute.For<ISender>();
        sender
            .Send(Arg.Any<UpdateTenantProfileCommand>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedCommand = callInfo.Arg<UpdateTenantProfileCommand>();
                return new ValueTask<ErrorOr<TenantResponse>>(expectedResponse);
            });

        await using TestCustomerApiHost host = await TestCustomerApiHost.StartAsync(
            sender,
            TenantContextAccessorFactory.Create(tenantIdFromContext));

        using HttpRequestMessage request = new(HttpMethod.Patch, "/customer/v1/Tenants/me")
        {
            Content = JsonContent.Create(new
            {
                name = "Tenant Context Updated",
                plan = "Enterprise",
            }),
        };

        request.WithAuthenticatedUser().WithTenantIdClaim(tenantIdFromClaim).WithScopes("tenant:update");

        // Act
        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        capturedCommand.ShouldNotBeNull();
        capturedCommand.TenantId.ShouldBe(tenantIdFromContext);
        capturedCommand.Name.ShouldBe("Tenant Context Updated");
        capturedCommand.Plan.ShouldBe("Enterprise");

        TenantResponse? responseBody = await response.Content
            .ReadFromJsonAsync<TenantResponse>(cancellationToken: TestContext.Current.CancellationToken);

        responseBody.ShouldNotBeNull();
        responseBody.Id.ShouldBe(expectedResponse.Id);
        responseBody.Name.ShouldBe(expectedResponse.Name);
        responseBody.Plan.ShouldBe(expectedResponse.Plan);
    }

    [Fact]
    public async Task PatchCurrentTenantProfile_ShouldReturn400_WhenPayloadHasNoUpdatableFields()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        ISender sender = Substitute.For<ISender>();
        await using TestCustomerApiHost host = await TestCustomerApiHost.StartAsync(sender);

        using HttpRequestMessage request = new(HttpMethod.Patch, "/customer/v1/Tenants/me")
        {
            Content = JsonContent.Create(new
            {
            }),
        };

        request.WithAuthenticatedUser().WithTenantIdClaim(tenantId).WithScopes("tenant:update");

        // Act
        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        string responseBody = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        responseBody.ShouldContain("At least one profile field must be provided");
    }

    [Fact]
    public async Task PatchCurrentTenantProfile_ShouldReturn400_WithDowngradeErrorCode_WhenPlanIsDowngraded()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        ISender sender = Substitute.For<ISender>();

        sender
            .Send(Arg.Any<UpdateTenantProfileCommand>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<ErrorOr<TenantResponse>>(
                Error.Validation("Tenant.Plan.DowngradeNotAllowed", "Tenant plan downgrades require a dedicated workflow")));

        await using TestCustomerApiHost host = await TestCustomerApiHost.StartAsync(sender);

        using HttpRequestMessage request = new(HttpMethod.Patch, "/customer/v1/Tenants/me")
        {
            Content = JsonContent.Create(new
            {
                plan = "Business",
            }),
        };

        request.WithAuthenticatedUser().WithTenantIdClaim(tenantId).WithScopes("tenant:update");

        // Act
        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        string responseBody = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        ResponseContainsValidationKey(responseBody, "Tenant.Plan.DowngradeNotAllowed").ShouldBeTrue();
    }

    private static bool ResponseContainsValidationKey(string json, string key)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("errors", out JsonElement errorsElement) ||
            errorsElement.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        foreach (JsonProperty property in errorsElement.EnumerateObject())
        {
            if (string.Equals(property.Name, key, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
#pragma warning restore CA2012

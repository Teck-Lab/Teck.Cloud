using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Customer.Application.Tenants.Features.UpdateTenantProfile.V1;
using Customer.Application.Tenants.Responses;
using Customer.IntegrationTests.TestSupport;
using ErrorOr;
using Mediator;
using NSubstitute;
using Shouldly;
#pragma warning disable CA2012

namespace Customer.IntegrationTests.Endpoints.Tenants;

public sealed class AdminUpdateTenantProfileEndpointIntegrationTests
{
    [Fact]
    public async Task UpdateTenantProfile_ShouldReturn401_WhenRequestIsUnauthenticated()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        ISender sender = Substitute.For<ISender>();
        await using TestCustomerApiHost host = await TestCustomerApiHost.StartAsync(sender);

        using HttpRequestMessage request = new(HttpMethod.Patch, $"/customer/v1/admin/Tenants/{tenantId:D}")
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
    public async Task UpdateTenantProfile_ShouldReturn403_WhenAuthenticatedButMissingScopeClaim()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        ISender sender = Substitute.For<ISender>();
        await using TestCustomerApiHost host = await TestCustomerApiHost.StartAsync(sender);

        using HttpRequestMessage request = new(HttpMethod.Patch, $"/customer/v1/admin/Tenants/{tenantId:D}")
        {
            Content = JsonContent.Create(new
            {
                name = "Tenant Renamed",
            }),
        };

        request.WithAuthenticatedUser();

        // Act
        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateTenantProfile_ShouldReturn400_WhenPayloadHasNoUpdatableFields()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        ISender sender = Substitute.For<ISender>();
        await using TestCustomerApiHost host = await TestCustomerApiHost.StartAsync(sender);

        using HttpRequestMessage request = new(HttpMethod.Patch, $"/customer/v1/admin/Tenants/{tenantId:D}")
        {
            Content = JsonContent.Create(new
            {
            }),
        };

        request.WithAuthenticatedUser().WithScopes("tenant:update");

        // Act
        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        string responseBody = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        responseBody.ShouldContain("At least one profile field must be provided");
    }

    [Fact]
    public async Task UpdateTenantProfile_ShouldReturn404_WhenTenantDoesNotExist()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        ISender sender = Substitute.For<ISender>();

        sender
            .Send(Arg.Any<UpdateTenantProfileCommand>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<ErrorOr<TenantResponse>>(
                Error.NotFound("Tenant.NotFound", $"Tenant with ID '{tenantId}' not found")));

        await using TestCustomerApiHost host = await TestCustomerApiHost.StartAsync(sender);

        using HttpRequestMessage request = new(HttpMethod.Patch, $"/customer/v1/admin/Tenants/{tenantId:D}")
        {
            Content = JsonContent.Create(new
            {
                name = "Tenant Renamed",
            }),
        };

        request.WithAuthenticatedUser().WithScopes("tenant:update");

        // Act
        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateTenantProfile_ShouldReturn400_WithDowngradeErrorCode_WhenPlanIsDowngraded()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        ISender sender = Substitute.For<ISender>();

        sender
            .Send(Arg.Any<UpdateTenantProfileCommand>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<ErrorOr<TenantResponse>>(
                Error.Validation("Tenant.Plan.DowngradeNotAllowed", "Tenant plan downgrades require a dedicated workflow")));

        await using TestCustomerApiHost host = await TestCustomerApiHost.StartAsync(sender);

        using HttpRequestMessage request = new(HttpMethod.Patch, $"/customer/v1/admin/Tenants/{tenantId:D}")
        {
            Content = JsonContent.Create(new
            {
                plan = "Business",
            }),
        };

        request.WithAuthenticatedUser().WithScopes("tenant:update");

        // Act
        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        string responseBody = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        ResponseContainsValidationKey(responseBody, "Tenant.Plan.DowngradeNotAllowed").ShouldBeTrue();
    }

    [Fact]
    public async Task UpdateTenantProfile_ShouldReturn200_AndDispatchCommandWithRouteTenantId()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        UpdateTenantProfileCommand? capturedCommand = null;

        TenantResponse expectedResponse = new()
        {
            Id = tenantId,
            Identifier = "tenant-admin-update",
            Name = "Tenant Admin Updated",
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

        await using TestCustomerApiHost host = await TestCustomerApiHost.StartAsync(sender);

        using HttpRequestMessage request = new(HttpMethod.Patch, $"/customer/v1/admin/Tenants/{tenantId:D}")
        {
            Content = JsonContent.Create(new
            {
                name = "Tenant Admin Updated",
                plan = "Enterprise",
            }),
        };

        request.WithAuthenticatedUser().WithScopes("tenant:update");

        // Act
        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        capturedCommand.ShouldNotBeNull();
        capturedCommand.TenantId.ShouldBe(tenantId);
        capturedCommand.Name.ShouldBe("Tenant Admin Updated");
        capturedCommand.Plan.ShouldBe("Enterprise");

        TenantResponse? responseBody = await response.Content
            .ReadFromJsonAsync<TenantResponse>(cancellationToken: TestContext.Current.CancellationToken);

        responseBody.ShouldNotBeNull();
        responseBody.Id.ShouldBe(expectedResponse.Id);
        responseBody.Name.ShouldBe(expectedResponse.Name);
        responseBody.Plan.ShouldBe(expectedResponse.Plan);
        responseBody.IsActive.ShouldBeTrue();
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

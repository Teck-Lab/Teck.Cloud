using System.Net;
using System.Text.Json;
using Customer.Application.Tenants.Features.GetTenantById.V1;
using Customer.Application.Tenants.Responses;
using Customer.IntegrationTests.TestSupport;
using ErrorOr;
using Mediator;
using NSubstitute;
using Shouldly;
#pragma warning disable CA2012

namespace Customer.IntegrationTests.Endpoints.Tenants;

public sealed class CurrentTenantServiceReadinessEndpointIntegrationTests
{
    [Fact]
    public async Task CheckCurrentTenantServiceReadiness_ShouldReturn401_WhenRequestIsUnauthenticated()
    {
        // Arrange
        ISender sender = Substitute.For<ISender>();
        await using TestCustomerApiHost host = await TestCustomerApiHost.StartAsync(sender);

        // Act
        HttpResponseMessage response = await host.Client.GetAsync(
            new Uri("/customer/v1/Tenants/me/Services/customer/Readiness", UriKind.Relative),
            TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CheckCurrentTenantServiceReadiness_ShouldReturn403_WhenAuthenticatedButMissingScopeClaim()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        ISender sender = Substitute.For<ISender>();
        await using TestCustomerApiHost host = await TestCustomerApiHost.StartAsync(sender);

        using HttpRequestMessage request = new(HttpMethod.Get, "/customer/v1/Tenants/me/Services/customer/Readiness");
        request.WithAuthenticatedUser().WithTenantIdClaim(tenantId);

        // Act
        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CheckCurrentTenantServiceReadiness_ShouldReturn400_WhenTenantContextIsMissing()
    {
        // Arrange
        ISender sender = Substitute.For<ISender>();
        await using TestCustomerApiHost host = await TestCustomerApiHost.StartAsync(sender);

        using HttpRequestMessage request = new(HttpMethod.Get, "/customer/v1/Tenants/me/Services/customer/Readiness");
        request.WithAuthenticatedUser().WithScopes("tenant:list");

        // Act
        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        string responseBody = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        ResponseContainsValidationKey(responseBody, "Tenant.Context").ShouldBeTrue();
    }

    [Fact]
    public async Task CheckCurrentTenantServiceReadiness_ShouldReturnReadyTrue_WhenTenantIsActiveAndServiceMetadataIsConfigured()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        GetTenantByIdQuery? capturedQuery = null;

        ISender sender = Substitute.For<ISender>();
        sender
            .Send(Arg.Any<GetTenantByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedQuery = callInfo.Arg<GetTenantByIdQuery>();
                return new ValueTask<ErrorOr<TenantResponse>>(new TenantResponse
                {
                    Id = tenantId,
                    Identifier = "tenant-readiness",
                    Name = "Tenant Readiness",
                    Plan = "Business",
                    DatabaseStrategy = "Dedicated",
                    IsActive = true,
                    Databases =
                    [
                        new TenantDatabaseMetadataResponse
                        {
                            ServiceName = "customer",
                            WriteEnvVarKey = "ConnectionStrings__Tenants__tenant-readiness__Write",
                            ReadEnvVarKey = "ConnectionStrings__Tenants__tenant-readiness__Read",
                            HasSeparateReadDatabase = true,
                        },
                    ],
                });
            });

        await using TestCustomerApiHost host = await TestCustomerApiHost.StartAsync(sender);

        using HttpRequestMessage request = new(HttpMethod.Get, "/customer/v1/Tenants/me/Services/customer/Readiness");
        request.WithAuthenticatedUser().WithTenantIdClaim(tenantId).WithScopes("tenant:list");

        // Act
        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        capturedQuery.ShouldNotBeNull();
        capturedQuery.TenantId.ShouldBe(tenantId);

        bool ready = await ReadReadyAsync(response, TestContext.Current.CancellationToken);
        ready.ShouldBeTrue();
    }

    [Fact]
    public async Task CheckCurrentTenantServiceReadiness_ShouldReturnReadyFalse_WhenServiceMetadataDoesNotExist()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();

        ISender sender = Substitute.For<ISender>();
        sender
            .Send(Arg.Any<GetTenantByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<ErrorOr<TenantResponse>>(new TenantResponse
            {
                Id = tenantId,
                Identifier = "tenant-readiness",
                Name = "Tenant Readiness",
                Plan = "Business",
                DatabaseStrategy = "Dedicated",
                IsActive = true,
                Databases =
                [
                    new TenantDatabaseMetadataResponse
                    {
                        ServiceName = "billing",
                        WriteEnvVarKey = "ConnectionStrings__Tenants__tenant-readiness__Write",
                        ReadEnvVarKey = null,
                        HasSeparateReadDatabase = false,
                    },
                ],
            }));

        await using TestCustomerApiHost host = await TestCustomerApiHost.StartAsync(sender);

        using HttpRequestMessage request = new(HttpMethod.Get, "/customer/v1/Tenants/me/Services/customer/Readiness");
        request.WithAuthenticatedUser().WithTenantIdClaim(tenantId).WithScopes("tenant:list");

        // Act
        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        bool ready = await ReadReadyAsync(response, TestContext.Current.CancellationToken);
        ready.ShouldBeFalse();
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

    private static async Task<bool> ReadReadyAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        string json = await response.Content.ReadAsStringAsync(cancellationToken);
        using JsonDocument document = JsonDocument.Parse(json);

        if (!document.RootElement.TryGetProperty("ready", out JsonElement readyElement) ||
            readyElement.ValueKind != JsonValueKind.True && readyElement.ValueKind != JsonValueKind.False)
        {
            return false;
        }

        return readyElement.GetBoolean();
    }
}
#pragma warning restore CA2012

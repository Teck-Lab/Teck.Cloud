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

public sealed class AdminServiceReadinessEndpointIntegrationTests
{
    [Fact]
    public async Task CheckServiceReadiness_ShouldReturn401_WhenRequestIsUnauthenticated()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        ISender sender = Substitute.For<ISender>();
        await using TestCustomerApiHost host = await TestCustomerApiHost.StartAsync(sender);

        // Act
        HttpResponseMessage response = await host.Client.GetAsync(
            new Uri($"/customer/v1/admin/Tenants/{tenantId:D}/Services/customer/Readiness", UriKind.Relative),
            TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CheckServiceReadiness_ShouldReturn403_WhenAuthenticatedButMissingScopeClaim()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        ISender sender = Substitute.For<ISender>();
        await using TestCustomerApiHost host = await TestCustomerApiHost.StartAsync(sender);

        using HttpRequestMessage request = new(
            HttpMethod.Get,
            $"/customer/v1/admin/Tenants/{tenantId:D}/Services/customer/Readiness");

        request.WithAuthenticatedUser();

        // Act
        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CheckServiceReadiness_ShouldReturn404_WhenTenantDoesNotExist()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        ISender sender = Substitute.For<ISender>();

        sender
            .Send(Arg.Any<GetTenantByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<ErrorOr<TenantResponse>>(
                Error.NotFound("Tenant.NotFound", $"Tenant with ID '{tenantId}' not found")));

        await using TestCustomerApiHost host = await TestCustomerApiHost.StartAsync(sender);

        using HttpRequestMessage request = new(
            HttpMethod.Get,
            $"/customer/v1/admin/Tenants/{tenantId:D}/Services/customer/Readiness");

        request.WithAuthenticatedUser().WithScopes("tenant:list");

        // Act
        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CheckServiceReadiness_ShouldReturnReadyTrue_WhenTenantIsActiveAndServiceMetadataIsConfigured()
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

        using HttpRequestMessage request = new(
            HttpMethod.Get,
            $"/customer/v1/admin/Tenants/{tenantId:D}/Services/customer/Readiness");

        request.WithAuthenticatedUser().WithScopes("tenant:list");

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
    public async Task CheckServiceReadiness_ShouldReturnReadyFalse_WhenTenantIsInactive()
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
                IsActive = false,
                Databases =
                [
                    new TenantDatabaseMetadataResponse
                    {
                        ServiceName = "customer",
                        WriteEnvVarKey = "ConnectionStrings__Tenants__tenant-readiness__Write",
                        ReadEnvVarKey = null,
                        HasSeparateReadDatabase = false,
                    },
                ],
            }));

        await using TestCustomerApiHost host = await TestCustomerApiHost.StartAsync(sender);

        using HttpRequestMessage request = new(
            HttpMethod.Get,
            $"/customer/v1/admin/Tenants/{tenantId:D}/Services/customer/Readiness");

        request.WithAuthenticatedUser().WithScopes("tenant:list");

        // Act
        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        bool ready = await ReadReadyAsync(response, TestContext.Current.CancellationToken);
        ready.ShouldBeFalse();
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

using System.Net;
using System.Net.Http.Json;
using Customer.Application.Tenants.Features.ActivateTenant.V1;
using Customer.Application.Tenants.Features.DeactivateTenant.V1;
using Customer.Application.Tenants.Responses;
using Customer.IntegrationTests.TestSupport;
using ErrorOr;
using Mediator;
using NSubstitute;
using Shouldly;
#pragma warning disable CA2012

namespace Customer.IntegrationTests.Endpoints.Tenants;

public sealed class AdminTenantActivationEndpointsIntegrationTests
{
    [Fact]
    public async Task ActivateTenant_ShouldReturn401_WhenRequestIsUnauthenticated()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        ISender sender = Substitute.For<ISender>();
        await using TestCustomerApiHost host = await TestCustomerApiHost.StartAsync(sender);

        // Act
        HttpResponseMessage response = await host.Client.PutAsJsonAsync(
            new Uri($"/customer/v1/admin/Tenants/{tenantId:D}/activate", UriKind.Relative),
            new { },
            TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeactivateTenant_ShouldReturn401_WhenRequestIsUnauthenticated()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        ISender sender = Substitute.For<ISender>();
        await using TestCustomerApiHost host = await TestCustomerApiHost.StartAsync(sender);

        // Act
        HttpResponseMessage response = await host.Client.PutAsJsonAsync(
            new Uri($"/customer/v1/admin/Tenants/{tenantId:D}/deactivate", UriKind.Relative),
            new { },
            TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ActivateTenant_ShouldReturn403_WhenAuthenticatedButMissingScopeClaim()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        ISender sender = Substitute.For<ISender>();
        await using TestCustomerApiHost host = await TestCustomerApiHost.StartAsync(sender);

        using HttpRequestMessage request = new(HttpMethod.Put, $"/customer/v1/admin/Tenants/{tenantId:D}/activate")
        {
            Content = JsonContent.Create(new { }),
        };
        request.WithAuthenticatedUser();

        // Act
        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeactivateTenant_ShouldReturn403_WhenAuthenticatedButMissingScopeClaim()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        ISender sender = Substitute.For<ISender>();
        await using TestCustomerApiHost host = await TestCustomerApiHost.StartAsync(sender);

        using HttpRequestMessage request = new(HttpMethod.Put, $"/customer/v1/admin/Tenants/{tenantId:D}/deactivate")
        {
            Content = JsonContent.Create(new { }),
        };
        request.WithAuthenticatedUser();

        // Act
        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ActivateTenant_ShouldReturn404_WhenTenantDoesNotExist()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        ISender sender = Substitute.For<ISender>();

        sender
            .Send(Arg.Any<ActivateTenantCommand>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<ErrorOr<TenantResponse>>(
                Error.NotFound("Tenant.NotFound", $"Tenant with ID '{tenantId}' not found")));

        await using TestCustomerApiHost host = await TestCustomerApiHost.StartAsync(sender);

        using HttpRequestMessage request = new(HttpMethod.Put, $"/customer/v1/admin/Tenants/{tenantId:D}/activate")
        {
            Content = JsonContent.Create(new { }),
        };
        request.WithAuthenticatedUser().WithScopes("tenant:update");

        // Act
        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeactivateTenant_ShouldReturn404_WhenTenantDoesNotExist()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        ISender sender = Substitute.For<ISender>();

        sender
            .Send(Arg.Any<DeactivateTenantCommand>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<ErrorOr<TenantResponse>>(
                Error.NotFound("Tenant.NotFound", $"Tenant with ID '{tenantId}' not found")));

        await using TestCustomerApiHost host = await TestCustomerApiHost.StartAsync(sender);

        using HttpRequestMessage request = new(HttpMethod.Put, $"/customer/v1/admin/Tenants/{tenantId:D}/deactivate")
        {
            Content = JsonContent.Create(new { }),
        };
        request.WithAuthenticatedUser().WithScopes("tenant:update");

        // Act
        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ActivateTenant_ShouldReturn200_AndDispatchCommandWithRouteTenantId()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        ActivateTenantCommand? capturedCommand = null;

        TenantResponse expectedResponse = new()
        {
            Id = tenantId,
            Identifier = "tenant-admin-activate",
            Name = "Tenant Admin Activate",
            Plan = "Business",
            DatabaseStrategy = "Dedicated",
            IsActive = true,
        };

        ISender sender = Substitute.For<ISender>();
        sender
            .Send(Arg.Any<ActivateTenantCommand>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedCommand = callInfo.Arg<ActivateTenantCommand>();
                return new ValueTask<ErrorOr<TenantResponse>>(expectedResponse);
            });

        await using TestCustomerApiHost host = await TestCustomerApiHost.StartAsync(sender);

        using HttpRequestMessage request = new(HttpMethod.Put, $"/customer/v1/admin/Tenants/{tenantId:D}/activate")
        {
            Content = JsonContent.Create(new { }),
        };
        request.WithAuthenticatedUser().WithScopes("tenant:update");

        // Act
        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        capturedCommand.ShouldNotBeNull();
        capturedCommand.TenantId.ShouldBe(tenantId);

        TenantResponse? responseBody = await response.Content
            .ReadFromJsonAsync<TenantResponse>(cancellationToken: TestContext.Current.CancellationToken);

        responseBody.ShouldNotBeNull();
        responseBody.Id.ShouldBe(expectedResponse.Id);
        responseBody.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task DeactivateTenant_ShouldReturn200_AndDispatchCommandWithRouteTenantId()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        DeactivateTenantCommand? capturedCommand = null;

        TenantResponse expectedResponse = new()
        {
            Id = tenantId,
            Identifier = "tenant-admin-deactivate",
            Name = "Tenant Admin Deactivate",
            Plan = "Business",
            DatabaseStrategy = "Dedicated",
            IsActive = false,
        };

        ISender sender = Substitute.For<ISender>();
        sender
            .Send(Arg.Any<DeactivateTenantCommand>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedCommand = callInfo.Arg<DeactivateTenantCommand>();
                return new ValueTask<ErrorOr<TenantResponse>>(expectedResponse);
            });

        await using TestCustomerApiHost host = await TestCustomerApiHost.StartAsync(sender);

        using HttpRequestMessage request = new(HttpMethod.Put, $"/customer/v1/admin/Tenants/{tenantId:D}/deactivate")
        {
            Content = JsonContent.Create(new { }),
        };
        request.WithAuthenticatedUser().WithScopes("tenant:update");

        // Act
        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        capturedCommand.ShouldNotBeNull();
        capturedCommand.TenantId.ShouldBe(tenantId);

        TenantResponse? responseBody = await response.Content
            .ReadFromJsonAsync<TenantResponse>(cancellationToken: TestContext.Current.CancellationToken);

        responseBody.ShouldNotBeNull();
        responseBody.Id.ShouldBe(expectedResponse.Id);
        responseBody.IsActive.ShouldBeFalse();
    }
}
#pragma warning restore CA2012

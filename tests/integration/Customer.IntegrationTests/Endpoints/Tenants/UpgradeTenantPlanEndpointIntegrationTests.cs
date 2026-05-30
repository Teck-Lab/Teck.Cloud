using System.Net;
using System.Net.Http.Json;
using Customer.Application.Tenants.Features.UpgradeTenantPlan.V1;
using Customer.IntegrationTests.TestSupport;
using ErrorOr;
using Mediator;
using NSubstitute;
using Shouldly;
#pragma warning disable CA2012

namespace Customer.IntegrationTests.Endpoints.Tenants;

public sealed class UpgradeTenantPlanEndpointIntegrationTests
{
    [Fact]
    public async Task UpgradeTenantPlan_ShouldReturn200_WhenCommandSucceeds()
    {
        // Arrange
        UpgradeTenantPlanCommand? capturedCommand = null;
        Guid tenantId = Guid.NewGuid();

        ISender sender = Substitute.For<ISender>();
        sender
            .Send(Arg.Any<UpgradeTenantPlanCommand>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedCommand = callInfo.Arg<UpgradeTenantPlanCommand>();
                return new ValueTask<ErrorOr<Success>>(Result.Success);
            });

        await using TestCustomerApiHost host = await TestCustomerApiHost.StartAsync(sender);

        using HttpRequestMessage request = new(HttpMethod.Post, $"/customer/v1/admin/Tenants/{tenantId:D}/plan/upgrade")
        {
            Content = JsonContent.Create(new
            {
                targetPlan = "Business",
                currency = "USD",
            }),
        };
        request.WithAuthenticatedUser().WithScopes("tenant:update");

        // Act
        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        capturedCommand.ShouldNotBeNull();
        capturedCommand.TenantId.ShouldBe(tenantId);
        capturedCommand.TargetPlan.ShouldBe("Business");
        capturedCommand.Currency.ShouldBe("USD");
    }

    [Fact]
    public async Task UpgradeTenantPlan_ShouldReturn400_WhenMediatorReturnsValidationError()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        ISender sender = Substitute.For<ISender>();
        sender
            .Send(Arg.Any<UpgradeTenantPlanCommand>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<ErrorOr<Success>>(
                Error.Validation("Tenant.Plan.Invalid", "Target plan is invalid.")));

        await using TestCustomerApiHost host = await TestCustomerApiHost.StartAsync(sender);

        using HttpRequestMessage request = new(HttpMethod.Post, $"/customer/v1/admin/Tenants/{tenantId:D}/plan/upgrade")
        {
            Content = JsonContent.Create(new
            {
                targetPlan = "Invalid",
                currency = "USD",
            }),
        };
        request.WithAuthenticatedUser().WithScopes("tenant:update");

        // Act
        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpgradeTenantPlan_ShouldReturn401_WhenRequestIsUnauthenticated()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        ISender sender = Substitute.For<ISender>();
        await using TestCustomerApiHost host = await TestCustomerApiHost.StartAsync(sender);

        using HttpRequestMessage request = new(HttpMethod.Post, $"/customer/v1/admin/Tenants/{tenantId:D}/plan/upgrade")
        {
            Content = JsonContent.Create(new
            {
                targetPlan = "Business",
                currency = "USD",
            }),
        };

        // Act
        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}

#pragma warning restore CA2012

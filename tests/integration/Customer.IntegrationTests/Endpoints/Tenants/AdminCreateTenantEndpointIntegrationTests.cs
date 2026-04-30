using System.Net;
using System.Net.Http.Json;
using Customer.Application.Tenants.Features.CreateTenant.V1;
using Customer.Application.Tenants.Responses;
using Customer.IntegrationTests.TestSupport;
using ErrorOr;
using Mediator;
using NSubstitute;
using Shouldly;
#pragma warning disable CA2012

namespace Customer.IntegrationTests.Endpoints.Tenants;

public sealed class AdminCreateTenantEndpointIntegrationTests
{
    [Fact]
    public async Task CreateTenant_ShouldReturn401_WhenRequestIsUnauthenticated()
    {
        // Arrange
        ISender sender = Substitute.For<ISender>();
        await using TestCustomerApiHost host = await TestCustomerApiHost.StartAsync(sender);

        using HttpRequestMessage request = new(HttpMethod.Post, "/customer/v1/admin/Tenants")
        {
            Content = JsonContent.Create(new
            {
                identifier = "tenant-alpha",
                name = "Tenant Alpha",
                plan = "Business",
                databaseStrategy = "Dedicated",
            }),
        };

        // Act
        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateTenant_ShouldReturn403_WhenAuthenticatedButMissingScopeClaim()
    {
        // Arrange
        ISender sender = Substitute.For<ISender>();
        await using TestCustomerApiHost host = await TestCustomerApiHost.StartAsync(sender);

        using HttpRequestMessage request = new(HttpMethod.Post, "/customer/v1/admin/Tenants")
        {
            Content = JsonContent.Create(new
            {
                identifier = "tenant-alpha",
                name = "Tenant Alpha",
                plan = "Business",
                databaseStrategy = "Dedicated",
            }),
        };

        request.WithAuthenticatedUser();

        // Act
        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateTenant_ShouldReturn400_WhenPayloadIsInvalid()
    {
        // Arrange
        ISender sender = Substitute.For<ISender>();
        await using TestCustomerApiHost host = await TestCustomerApiHost.StartAsync(sender);

        using HttpRequestMessage request = new(HttpMethod.Post, "/customer/v1/admin/Tenants")
        {
            Content = JsonContent.Create(new
            {
            }),
        };

        request.WithAuthenticatedUser().WithScopes("tenant:create");

        // Act
        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        string responseBody = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        responseBody.ShouldContain("Identifier is required");
        responseBody.ShouldContain("Name is required");
        responseBody.ShouldContain("Plan is required");
        responseBody.ShouldContain("DatabaseStrategy is required");
    }

    [Fact]
    public async Task CreateTenant_ShouldReturn409_WhenCommandReturnsConflict()
    {
        // Arrange
        ISender sender = Substitute.For<ISender>();
        sender
            .Send(Arg.Any<CreateTenantCommand>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<ErrorOr<TenantResponse>>(
                Error.Conflict("Tenant.AlreadyExists", "Tenant already exists")));

        await using TestCustomerApiHost host = await TestCustomerApiHost.StartAsync(sender);

        using HttpRequestMessage request = new(HttpMethod.Post, "/customer/v1/admin/Tenants")
        {
            Content = JsonContent.Create(new
            {
                identifier = "tenant-alpha",
                name = "Tenant Alpha",
                plan = "Business",
                databaseStrategy = "Dedicated",
            }),
        };

        request.WithAuthenticatedUser().WithScopes("tenant:create");

        // Act
        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CreateTenant_ShouldReturn201_AndDispatchCommandWithMappedValues()
    {
        // Arrange
        CreateTenantCommand? capturedCommand = null;
        Guid createdTenantId = Guid.NewGuid();

        TenantResponse createdTenant = new()
        {
            Id = createdTenantId,
            Identifier = "tenant-alpha",
            Name = "Tenant Alpha",
            Plan = "Business",
            DatabaseStrategy = "Dedicated",
            IsActive = true,
        };

        ISender sender = Substitute.For<ISender>();
        sender
            .Send(Arg.Any<CreateTenantCommand>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedCommand = callInfo.Arg<CreateTenantCommand>();
                return new ValueTask<ErrorOr<TenantResponse>>(createdTenant);
            });

        await using TestCustomerApiHost host = await TestCustomerApiHost.StartAsync(sender);

        using HttpRequestMessage request = new(HttpMethod.Post, "/customer/v1/admin/Tenants")
        {
            Content = JsonContent.Create(new
            {
                identifier = "tenant-alpha",
                name = "Tenant Alpha",
                plan = "Business",
                databaseStrategy = "Dedicated",
            }),
        };

        request.WithAuthenticatedUser().WithScopes("tenant:create");

        // Act
        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.Location.ShouldNotBeNull();
        response.Headers.Location!.ToString().ShouldBe($"/customer/v1/admin/Tenants/{createdTenantId:D}");

        capturedCommand.ShouldNotBeNull();
        capturedCommand.Identifier.ShouldBe("tenant-alpha");
        capturedCommand.Profile.Name.ShouldBe("Tenant Alpha");
        capturedCommand.Profile.Plan.ShouldBe("Business");
        capturedCommand.Database.DatabaseStrategy.Name.ShouldBe("Dedicated");
        capturedCommand.Database.DatabaseProvider.Name.ShouldBe("PostgreSQL");

        TenantResponse? responseBody = await response.Content
            .ReadFromJsonAsync<TenantResponse>(cancellationToken: TestContext.Current.CancellationToken);

        responseBody.ShouldNotBeNull();
        responseBody.Id.ShouldBe(createdTenantId);
        responseBody.Identifier.ShouldBe("tenant-alpha");
        responseBody.Name.ShouldBe("Tenant Alpha");
        responseBody.Plan.ShouldBe("Business");
        responseBody.IsActive.ShouldBeTrue();
    }
}
#pragma warning restore CA2012

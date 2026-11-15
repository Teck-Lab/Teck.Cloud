using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using SharedKernel.Core.MultiTenancy;
using SharedKernel.Infrastructure.MultiTenancy;
using Shouldly;
using Xunit;

namespace SharedKernel.Infrastructure.UnitTests;

/// <summary>Tests Keycloak organization claim interpretation at the operation boundary.</summary>
public sealed partial class IngressTests
{
    /// <summary>Missing UUIDs, multiple memberships and malformed JSON cannot initialize tenant scope.</summary>
    /// <param name="organization">The signed claim payload being evaluated.</param>
    /// <returns>The verification task.</returns>
    [Theory]
    [InlineData("{}")]
    [InlineData("{\"alpha\":{}}")]
    [InlineData("{\"alpha\":{\"id\":\"alpha\"},\"beta\":{\"id\":\"beta\"}}")]
    [InlineData("{invalid-json")]
    [InlineData("[\"alpha\"]")]
    public async Task AmbiguousOrMalformedOrganizationFailsClosed(string organization)
    {
        var services = new ServiceCollection();
        var directory = Substitute.For<ITenantDirectory>();
        _ = services.AddSingleton(directory);
        await using var provider = services.BuildServiceProvider();
        var called = false;
        var middleware = new OperationScopeMiddleware(_ =>
        {
            called = true;
            return Task.CompletedTask;
        });
        var http = new DefaultHttpContext
        {
            RequestServices = provider,
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("organization", organization, "JSON"), new Claim("tenant_id", "alpha")], "validated-test-jwt")),
        };
        http.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(OperationScope.Tenant), "tenant"));
        http.Request.Headers["x-tenant-id"] = "alpha";
        var operation = new OperationContext();
        await middleware.InvokeAsync(http, operation).ConfigureAwait(true);
        called.ShouldBeFalse();
        http.Response.StatusCode.ShouldBe(403);
        _ = Should.Throw<InvalidOperationException>(() => operation.Scope);
        _ = await directory.DidNotReceive().IsActiveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).ConfigureAwait(true);
    }

    /// <summary>A legacy tenant claim or alias-only organization does not replace a selected organization UUID.</summary>
    /// <param name="claimType">The identity claim type.</param>
    /// <returns>The verification task.</returns>
    [Theory]
    [InlineData("tenant_id")]
    [InlineData("organization")]
    public async Task LegacyClaimsAndAliasesCannotGrantTenantScope(string claimType)
    {
        var called = false;
        var middleware = new OperationScopeMiddleware(_ =>
        {
            called = true;
            return Task.CompletedTask;
        });
        var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(claimType, "alpha")], "validated-test-jwt")) };
        http.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(OperationScope.Tenant), "tenant"));
        await middleware.InvokeAsync(http, new OperationContext()).ConfigureAwait(true);
        called.ShouldBeFalse();
        http.Response.StatusCode.ShouldBe(403);
    }
}

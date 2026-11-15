using System.Security.Claims;
using System.Text.Json.Serialization;
using ErrorOr;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using SharedKernel.Core.MultiTenancy;
using SharedKernel.Infrastructure.Http;
using SharedKernel.Infrastructure.MultiTenancy;
using Shouldly;
using Xunit;

namespace SharedKernel.Infrastructure.UnitTests;

/// <summary>Tests trust decisions and standard result mapping without external services.</summary>
public sealed partial class IngressTests
{
    /// <summary>Raw headers do not establish authority, while an authenticated active tenant does.</summary>
    /// <returns>The verification task.</returns>
    [Fact]
    public async Task TenantIngressRejectsHeaderOnlyAndInactiveIdentities()
    {
        var services = new ServiceCollection();
        var directory = Substitute.For<ITenantDirectory>();
        _ = directory.IsActiveAsync("alpha", Arg.Any<CancellationToken>()).Returns(returnThis: true);
        _ = directory.IsActiveAsync("inactive", Arg.Any<CancellationToken>()).Returns(returnThis: false);
        _ = services.AddSingleton(directory);
        await using var provider = services.BuildServiceProvider();
        var called = false;
        var middleware = new OperationScopeMiddleware(_ =>
        {
            called = true;
            return Task.CompletedTask;
        });
        var http = new DefaultHttpContext { RequestServices = provider };
        http.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(OperationScope.Tenant), "tenant"));
        http.Request.Headers["x-tenant-id"] = "alpha";
        var operation = new OperationContext();
        await middleware.InvokeAsync(http, operation).ConfigureAwait(true);
        called.ShouldBeFalse();
        http.Response.StatusCode.ShouldBe(403);
        _ = Should.Throw<InvalidOperationException>(() => operation.Scope).ShouldBeOfType<InvalidOperationException>();
        http.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("organization", """{"inactive":{"id":"inactive"}}""", "JSON")], "test"));
        await middleware.InvokeAsync(http, operation).ConfigureAwait(true);
        called.ShouldBeFalse();
        http.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("organization", """{"alpha":{"id":"alpha"}}""", "JSON"), new Claim("sub", "actor")], "test"));
        await middleware.InvokeAsync(http, operation).ConfigureAwait(true);
        called.ShouldBeTrue();
        operation.Scope.TenantId.ShouldBe("alpha");
        operation.ActorId.ShouldBe("actor");
        _ = await directory.Received(1).IsActiveAsync("inactive", Arg.Any<CancellationToken>()).ConfigureAwait(true);
        _ = await directory.Received(1).IsActiveAsync("alpha", Arg.Any<CancellationToken>()).ConfigureAwait(true);
    }

    /// <summary>A global endpoint does not need a tenant resolver or fabricate a tenant.</summary>
    /// <returns>The verification task.</returns>
    [Fact]
    public async Task GlobalIngressIsExplicitAndUnclassifiedRequestsRemainUnscoped()
    {
        var middleware = new OperationScopeMiddleware(_ => Task.CompletedTask);
        var http = new DefaultHttpContext();
        var operation = new OperationContext();
        await middleware.InvokeAsync(http, operation).ConfigureAwait(true);
        _ = Should.Throw<InvalidOperationException>(() => operation.Scope).ShouldBeOfType<InvalidOperationException>();
        http.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(OperationScope.Global), "global"));
        await middleware.InvokeAsync(http, operation).ConfigureAwait(true);
        operation.Scope.ShouldBe(DataScope.Global);
        OperationScope.Tenant.ShouldNotBeSameAs(OperationScope.Global);
    }

    /// <summary>Expected errors map to problem details; unexpected details are redacted.</summary>
    [Fact]
    public void ResultsKeepSuccessAndMapErrorsWithoutCustomEndpoints()
    {
        ErrorOr<Response> success = new Response("ok");
        var json = success.ToHttpResult(ApiJson.Default.Response).ShouldBeOfType<JsonHttpResult<Response>>();
        (json.Value?.Value).ShouldBe("ok");
        ErrorOr<Response> invalid = Error.Validation("name", "Required");
        var validation = invalid.ToHttpResult(ApiJson.Default.Response).ShouldBeOfType<ValidationProblem>();
        validation.ProblemDetails.Errors["name"].ShouldHaveSingleItem().ShouldBe("Required");
        ErrorOr<Response> failed = Error.Unexpected("Database", "private connection details");
        var problem = failed.ToHttpResult(ApiJson.Default.Response).ShouldBeOfType<ProblemHttpResult>();
        problem.StatusCode.ShouldBe(500);
        problem.ProblemDetails.Detail.ShouldBeNull();
        ErrorOr<Response> conflict = Error.Conflict("Duplicate", "Already exists");
        conflict.ToHttpResult(ApiJson.Default.Response).ShouldBeOfType<ProblemHttpResult>().StatusCode.ShouldBe(409);
    }

    /// <summary>Response DTO for generated serialization.</summary>
    /// <param name="Value">The result value.</param>
    public sealed record Response(string Value);

    [JsonSerializable(typeof(Response))]
    private sealed partial class ApiJson : JsonSerializerContext;
}

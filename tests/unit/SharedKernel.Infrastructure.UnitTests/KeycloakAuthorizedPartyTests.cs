using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using SharedKernel.Infrastructure.Auth;
using Shouldly;
using Xunit;

namespace SharedKernel.Infrastructure.UnitTests;

/// <summary>Trusted issuance clients cannot be replaced by missing, duplicate or unrelated claims.</summary>
public sealed class KeycloakAuthorizedPartyTests
{
    /// <summary>Client matching is exact and requires a single issuer principal claim.</summary>
    /// <param name="client">The validated token claim, if present.</param>
    /// <param name="expected">Expected policy result.</param>
    /// <returns>The policy evaluation.</returns>
    [Theory]
    [InlineData("admin-gateway", true)]
    [InlineData("second-admin", true)]
    [InlineData("customer-gateway", false)]
    [InlineData("ADMIN-GATEWAY", false)]
    [InlineData("admin-gateway ", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public async Task ExactAuthorizedClientIsRequired(string? client, bool expected)
    {
        var identity = new ClaimsIdentity("Bearer");
        if (client is not null)
        {
            identity.AddClaim(new Claim("azp", client));
        }

        (await EvaluateAsync(new ClaimsPrincipal(identity)).ConfigureAwait(true)).ShouldBe(expected);
    }

    /// <summary>Neither mixed identities nor duplicate claims can supply an accepted client.</summary>
    /// <returns>The policy evaluation.</returns>
    [Fact]
    public async Task AmbiguousAndUnauthenticatedClaimsFailClosed()
    {
        var claim = new Claim("azp", "admin-gateway");
        (await EvaluateAsync(new ClaimsPrincipal(new ClaimsIdentity([claim]))).ConfigureAwait(true)).ShouldBeFalse();
        (await EvaluateAsync(new ClaimsPrincipal([new ClaimsIdentity([claim], "Bearer"), new ClaimsIdentity("Other")])).ConfigureAwait(true)).ShouldBeFalse();
        (await EvaluateAsync(new ClaimsPrincipal(new ClaimsIdentity([claim, claim], "Bearer"))).ConfigureAwait(true)).ShouldBeFalse();
        (await EvaluateAsync(new ClaimsPrincipal(new ClaimsIdentity([claim, new Claim("azp", "customer-gateway")], "Bearer"))).ConfigureAwait(true)).ShouldBeFalse();
        (await EvaluateAsync(new ClaimsPrincipal([new ClaimsIdentity("Bearer"), new ClaimsIdentity([claim])])).ConfigureAwait(true)).ShouldBeFalse();
    }

    /// <summary>The trusted allowlist is validated and copied rather than retaining mutable caller state.</summary>
    /// <returns>The policy evaluation.</returns>
    [Fact]
    public async Task CompositionValidatesAndSnapshotsTheAllowlist()
    {
        _ = Should.Throw<ArgumentNullException>(() => KeycloakAuthorizedPartyExtensions.RequireKeycloakAuthorizedParty(null!, "admin-gateway"));
        _ = Should.Throw<ArgumentNullException>(() => new AuthorizationPolicyBuilder().RequireKeycloakAuthorizedParty(null!));
        _ = Should.Throw<ArgumentException>(() => new AuthorizationPolicyBuilder().RequireKeycloakAuthorizedParty());
        _ = Should.Throw<ArgumentException>(() => new AuthorizationPolicyBuilder().RequireKeycloakAuthorizedParty("admin-gateway", " "));
        string[] clients = ["admin-gateway"];
        var policy = new AuthorizationPolicyBuilder().RequireKeycloakAuthorizedParty(clients).Build();
        clients[0] = "customer-gateway";
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("azp", "admin-gateway")], "Bearer"));
        (await EvaluateAsync(principal, policy).ConfigureAwait(true)).ShouldBeTrue();
    }

    /// <summary>A competing success cannot override an explicit rejection.</summary>
    /// <returns>The policy evaluation.</returns>
    [Fact]
    public async Task RejectionCannotBeOverridden()
    {
        var policy = new AuthorizationPolicyBuilder().RequireKeycloakAuthorizedParty("admin-gateway").Build();
        var requirement = policy.Requirements.OfType<AssertionRequirement>().Single();
        var context = new AuthorizationHandlerContext(policy.Requirements, new ClaimsPrincipal(new ClaimsIdentity([new Claim("azp", "customer-gateway")], "Bearer")), resource: null);
        context.Succeed(requirement);
        await requirement.HandleAsync(context).ConfigureAwait(true);
        context.HasFailed.ShouldBeTrue();
    }

    private static async Task<bool> EvaluateAsync(ClaimsPrincipal principal, AuthorizationPolicy? policy = null)
    {
        policy ??= new AuthorizationPolicyBuilder().RequireKeycloakAuthorizedParty("admin-gateway", "second-admin").Build();
        var context = new AuthorizationHandlerContext(policy.Requirements, principal, resource: null);
        await new DenyAnonymousAuthorizationRequirement().HandleAsync(context).ConfigureAwait(false);
        await policy.Requirements.OfType<AssertionRequirement>().Single().HandleAsync(context).ConfigureAwait(false);
        return context.HasSucceeded;
    }
}

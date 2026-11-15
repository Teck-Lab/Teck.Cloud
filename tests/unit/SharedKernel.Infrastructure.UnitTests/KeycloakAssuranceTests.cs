using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using SharedKernel.Infrastructure.Auth;
using Shouldly;
using Xunit;

namespace SharedKernel.Infrastructure.UnitTests;

/// <summary>Checks exact signed assurance and original-authentication freshness boundaries.</summary>
public sealed class KeycloakAssuranceTests
{
    /// <summary>Fresh MFA succeeds; missing, weak, malformed, future and stale claims fail closed.</summary>
    /// <param name="acr">Signed assurance value.</param>
    /// <param name="authenticatedAt">Signed original authentication timestamp.</param>
    /// <param name="expected">Whether authorization must succeed.</param>
    /// <returns>The verification task.</returns>
    [Theory]
    [InlineData("2", "1700000000", true)]
    [InlineData("2", "1699999700", true)]
    [InlineData("2", "1699999699", false)]
    [InlineData("2", "1700000001", false)]
    [InlineData("1", "1700000000", false)]
    [InlineData("3", "1700000000", false)]
    [InlineData(null, "1700000000", false)]
    [InlineData("2", null, false)]
    [InlineData("2", "not-time", false)]
    [InlineData("2", "-1", false)]
    [InlineData("2", "1700000000.0", false)]
    [InlineData("2", "9223372036854775807", false)]
    public async Task AssuranceAndFreshnessAreBothRequired(string? acr, string? authenticatedAt, bool expected)
    {
        var identity = new ClaimsIdentity("Bearer");
        if (acr is not null)
        {
            identity.AddClaim(new Claim("acr", acr));
        }

        if (authenticatedAt is not null)
        {
            identity.AddClaim(new Claim("auth_time", authenticatedAt));
        }

        // A fresh token/refresh timestamp never replaces the original authentication time.
        identity.AddClaim(new Claim("iat", "1700000000"));
        (await EvaluateAsync(new ClaimsPrincipal(identity)).ConfigureAwait(true)).ShouldBe(expected);
    }

    /// <summary>Unverified and mixed identities cannot contribute claims to a successful decision.</summary>
    /// <returns>The verification task.</returns>
    [Fact]
    public async Task UnauthenticatedAndAmbiguousIdentitiesAreRejected()
    {
        var claims = new[] { new Claim("acr", "2"), new Claim("auth_time", "1700000000") };
        (await EvaluateAsync(new ClaimsPrincipal(new ClaimsIdentity(claims))).ConfigureAwait(true)).ShouldBeFalse();
        (await EvaluateAsync(new ClaimsPrincipal([new ClaimsIdentity(claims, "Bearer"), new ClaimsIdentity(claims, "Other")])).ConfigureAwait(true)).ShouldBeFalse();
        var duplicate = new ClaimsIdentity(claims, "Bearer");
        duplicate.AddClaim(new Claim("acr", "1"));
        (await EvaluateAsync(new ClaimsPrincipal(duplicate)).ConfigureAwait(true)).ShouldBeFalse();
        var duplicateTime = new ClaimsIdentity(claims, "Bearer");
        duplicateTime.AddClaim(new Claim("auth_time", "1700000000"));
        (await EvaluateAsync(new ClaimsPrincipal(duplicateTime)).ConfigureAwait(true)).ShouldBeFalse();
    }

    /// <summary>Invalid requirements fail at composition and registration remains idempotent.</summary>
    [Fact]
    public void CompositionRejectsAnEmptyAssuranceOrNonPositiveAge()
    {
        _ = Should.Throw<ArgumentException>(() => new KeycloakAssuranceRequirement(string.Empty, TimeSpan.FromMinutes(5)));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => new KeycloakAssuranceRequirement("2", TimeSpan.Zero));
        var services = new ServiceCollection();
        _ = services.AddKeycloakAssurance().AddKeycloakAssurance();
        using var provider = services.BuildServiceProvider();
        _ = provider.GetServices<IAuthorizationHandler>().ShouldHaveSingleItem();
        var policy = new AuthorizationPolicyBuilder().RequireKeycloakAssurance("2", TimeSpan.FromMinutes(5)).Build();
        _ = policy.Requirements.OfType<KeycloakAssuranceRequirement>().ShouldHaveSingleItem();
    }

    /// <summary>A different handler cannot override a rejected assurance decision.</summary>
    /// <returns>The verification task.</returns>
    [Fact]
    public async Task RejectionCannotBeOverriddenByAnotherHandler()
    {
        var requirement = new KeycloakAssuranceRequirement("2", TimeSpan.FromMinutes(5));
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("acr", "1"), new Claim("auth_time", "1700000000")], "Bearer"));
        var context = new AuthorizationHandlerContext([requirement], principal, resource: null);
        context.Succeed(requirement);
        await new KeycloakAssuranceHandler(TimeProvider.System).HandleAsync(context).ConfigureAwait(true);
        context.HasSucceeded.ShouldBeFalse();
        context.HasFailed.ShouldBeTrue();
    }

    private static async Task<bool> EvaluateAsync(ClaimsPrincipal principal)
    {
        var time = Substitute.For<TimeProvider>();
        _ = time.GetUtcNow().Returns(DateTimeOffset.FromUnixTimeSeconds(1700000000));
        var requirement = new KeycloakAssuranceRequirement("2", TimeSpan.FromMinutes(5));
        var context = new AuthorizationHandlerContext([requirement], principal, resource: null);
        await new KeycloakAssuranceHandler(time).HandleAsync(context).ConfigureAwait(false);
        return context.HasSucceeded;
    }
}

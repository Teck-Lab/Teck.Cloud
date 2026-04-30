using Customer.Api.Infrastructure.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using SharedKernel.Infrastructure.MultiTenant;
using Shouldly;
using System.Security.Claims;

namespace Customer.UnitTests.Infrastructure.MultiTenant;

public sealed class CurrentTenantResolverTests
{
    [Fact]
    public void TryResolveTenantId_ShouldResolveFromMultiTenantContext_WhenContextContainsGuid()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        HttpContext httpContext = CreateHttpContext();

        var tenantContextAccessor = Substitute.For<IMultiTenantContextAccessor<TenantDetails>>();
        tenantContextAccessor.MultiTenantContext.Returns(
            new MultiTenantContext<TenantDetails>(new TenantDetails { Id = tenantId.ToString("D") }));

        // Act
        bool resolved = CurrentTenantResolver.TryResolveTenantId(httpContext, tenantContextAccessor, out Guid resolvedTenantId);

        // Assert
        resolved.ShouldBeTrue();
        resolvedTenantId.ShouldBe(tenantId);
    }

    [Fact]
    public void TryResolveTenantId_ShouldFallbackToTenantClaim_WhenMultiTenantContextIsMissing()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        HttpContext httpContext = CreateHttpContext(tenantId.ToString("D"));

        // Act
        bool resolved = CurrentTenantResolver.TryResolveTenantId(httpContext, tenantContextAccessor: null, out Guid resolvedTenantId);

        // Assert
        resolved.ShouldBeTrue();
        resolvedTenantId.ShouldBe(tenantId);
    }

    [Fact]
    public void TryResolveTenantId_ShouldReturnFalse_WhenContextAndClaimsDoNotContainValidGuid()
    {
        // Arrange
        HttpContext httpContext = CreateHttpContext("not-a-guid");
        var tenantContextAccessor = Substitute.For<IMultiTenantContextAccessor<TenantDetails>>();
        tenantContextAccessor.MultiTenantContext.Returns(
            new MultiTenantContext<TenantDetails>(new TenantDetails { Id = "also-not-a-guid" }));

        // Act
        bool resolved = CurrentTenantResolver.TryResolveTenantId(httpContext, tenantContextAccessor, out Guid resolvedTenantId);

        // Assert
        resolved.ShouldBeFalse();
        resolvedTenantId.ShouldBe(Guid.Empty);
    }

    private static HttpContext CreateHttpContext(string? tenantIdClaimValue = null)
    {
        var httpContext = new DefaultHttpContext();

        if (!string.IsNullOrWhiteSpace(tenantIdClaimValue))
        {
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity([
                new Claim("tenant_id", tenantIdClaimValue),
            ], "TestAuth"));
        }

        return httpContext;
    }
}

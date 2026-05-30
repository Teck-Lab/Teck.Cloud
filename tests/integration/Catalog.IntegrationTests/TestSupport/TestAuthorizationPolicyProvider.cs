using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Catalog.IntegrationTests.TestSupport;

internal sealed class TestAuthorizationPolicyProvider(IOptions<AuthorizationOptions> options)
    : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider fallbackProvider = new(options);

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        AuthorizationPolicyBuilder policyBuilder = new(TestAuthHandler.SchemeName);
        _ = policyBuilder.RequireAuthenticatedUser();
        AuthorizationPolicy policy = policyBuilder.Build();
        return Task.FromResult<AuthorizationPolicy?>(policy);
    }

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => this.fallbackProvider.GetDefaultPolicyAsync();

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => this.fallbackProvider.GetFallbackPolicyAsync();
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Order.IntegrationTests.TestSupport;

internal sealed class TestAuthorizationPolicyProvider : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider fallbackProvider;

    public TestAuthorizationPolicyProvider(IOptions<AuthorizationOptions> options)
    {
        this.fallbackProvider = new DefaultAuthorizationPolicyProvider(options);
    }

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        AuthorizationPolicyBuilder policyBuilder = new(TestAuthHandler.SchemeName);
        _ = policyBuilder.RequireAuthenticatedUser();

        if (!string.IsNullOrWhiteSpace(policyName))
        {
            _ = policyBuilder.RequireAssertion(context =>
            {
                string? scopesValue = context.User.FindFirst("scope")?.Value;
                return !string.IsNullOrWhiteSpace(scopesValue);
            });
        }

        AuthorizationPolicy policy = policyBuilder.Build();
        return Task.FromResult<AuthorizationPolicy?>(policy);
    }

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync()
    {
        return this.fallbackProvider.GetDefaultPolicyAsync();
    }

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync()
    {
        return this.fallbackProvider.GetFallbackPolicyAsync();
    }
}

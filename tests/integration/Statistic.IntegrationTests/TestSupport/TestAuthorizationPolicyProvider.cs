// <copyright file="TestAuthorizationPolicyProvider.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Statistic.IntegrationTests.TestSupport;

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

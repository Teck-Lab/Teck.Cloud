using System.Net.Http.Headers;

namespace Customer.IntegrationTests.TestSupport;

internal static class TenantTestRequestExtensions
{
    public static HttpRequestMessage WithAuthenticatedUser(this HttpRequestMessage request)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "integration-token");
        return request;
    }

    public static HttpRequestMessage WithTenantIdClaim(this HttpRequestMessage request, Guid tenantId)
    {
        request.Headers.Add(TestAuthHandler.TenantIdClaimHeaderName, tenantId.ToString("D"));
        return request;
    }

    public static HttpRequestMessage WithScopes(this HttpRequestMessage request, params string[] scopes)
    {
        if (scopes.Length == 0)
        {
            return request;
        }

        request.Headers.Add(TestAuthHandler.ScopeHeaderName, string.Join(' ', scopes));
        return request;
    }
}

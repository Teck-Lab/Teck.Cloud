using System.Net.Http.Headers;

namespace Order.IntegrationTests.TestSupport;

internal static class TestRequestExtensions
{
    public static HttpRequestMessage WithAuthenticatedUser(this HttpRequestMessage request)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "integration-token");
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

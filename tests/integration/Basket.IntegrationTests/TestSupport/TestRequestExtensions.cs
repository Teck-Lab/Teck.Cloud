using System.Net.Http.Headers;

namespace Basket.IntegrationTests.TestSupport;

internal static class TestRequestExtensions
{
    public static HttpRequestMessage WithAuthenticatedUser(this HttpRequestMessage request)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "integration-token");
        return request;
    }
}

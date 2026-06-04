using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Location.IntegrationTests.TestSupport;
using Shouldly;

namespace Location.IntegrationTests.Endpoints.Service;

[Collection("LocationIntegrationTests")]
public sealed class ServiceEndpointsTenantValidationIntegrationTests
public sealed class ServiceEndpointsTenantValidationIntegrationTests
public sealed class ServiceEndpointsTenantValidationIntegrationTests
{
    [Fact]
    public async Task CreateLocationNode_ShouldReturnBadRequest_WhenTenantHeaderIsMissing()
    {
        await using TestLocationApiHost host = await TestLocationApiHost.StartAsync();

        using HttpRequestMessage request = new(HttpMethod.Post, "/location/v1/Service/LocationNodes")
        {
            Content = JsonContent.Create(new { name = "Store A", parentLocationNodeId = "parent-1" }),
        };

        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);
        string responseBody = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, responseBody);
        ShouldContainErrorCode(responseBody, "Location.LocationNode.TenantIdRequired");
    }

    [Fact]
    public async Task GetDisplayModels_ShouldReturnBadRequest_WhenTenantHeaderIsMissing()
    {
        await using TestLocationApiHost host = await TestLocationApiHost.StartAsync();

        HttpResponseMessage response = await host.Client.GetAsync(
            new Uri("/location/v1/Service/Displays", UriKind.Relative),
            TestContext.Current.CancellationToken);
        string responseBody = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, responseBody);
        ShouldContainErrorCode(responseBody, "Location.DisplayModels.TenantIdRequired");
    }

    private static void ShouldContainErrorCode(string responseBody, string errorCode)
    {
        responseBody.ShouldNotBeNullOrWhiteSpace();

        using JsonDocument document = JsonDocument.Parse(responseBody);

        if (!document.RootElement.TryGetProperty("errors", out JsonElement errorsElement))
        {
            responseBody.ShouldContain(errorCode);
            return;
        }

        if (errorsElement.ValueKind == JsonValueKind.Object)
        {
            errorsElement.TryGetProperty(errorCode, out _).ShouldBeTrue(responseBody);
            return;
        }

        if (errorsElement.ValueKind == JsonValueKind.Array)
        {
            bool hasErrorCode = errorsElement
                .EnumerateArray()
                .Any(entry =>
                    entry.TryGetProperty("name", out JsonElement nameElement)
                    && string.Equals(nameElement.GetString(), errorCode, StringComparison.Ordinal));

            hasErrorCode.ShouldBeTrue(responseBody);
            return;
        }

        responseBody.ShouldContain(errorCode);
    }
}

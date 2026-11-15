using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FastEndpoints.Testing;
using Shouldly;
using Xunit;

namespace SharedKernel.Infrastructure.IntegrationTests;

/// <summary>Runs real HTTP requests through the standard endpoint and hosting pipeline.</summary>
/// <param name="app">The FastEndpoints-managed application fixture.</param>
public sealed class ApiTests(ApiApp app) : TestBase<ApiApp>
{
    /// <summary>Generated endpoints, ErrorOr mapping, health and Scalar/OpenAPI work without storage services.</summary>
    /// <returns>The verification task.</returns>
    [Fact]
    public async Task HostServesStandardEndpointsAndDocumentation()
    {
        using var client = app.CreateClient();
        var cancellation = TestContext.Current.CancellationToken;
        using var success = await client.GetAsync(new Uri("/probe", UriKind.Relative), cancellation).ConfigureAwait(true);
        success.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var response = JsonDocument.Parse(await success.Content.ReadAsStringAsync(cancellation).ConfigureAwait(true));
        response.RootElement.GetProperty("scope").GetString().ShouldBe("global");
        using var rejected = await client.GetAsync(new Uri("/probe?reject=true", UriKind.Relative), cancellation).ConfigureAwait(true);
        rejected.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var problem = JsonDocument.Parse(await rejected.Content.ReadAsStringAsync(cancellation).ConfigureAwait(true));
        problem.RootElement.TryGetProperty("errors", out _).ShouldBeTrue();
        using var health = await client.GetAsync(new Uri("/health/live", UriKind.Relative), cancellation).ConfigureAwait(true);
        health.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var openApi = await client.GetAsync(new Uri("/openapi/v1.json", UriKind.Relative), cancellation).ConfigureAwait(true);
        openApi.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await openApi.Content.ReadAsStringAsync(cancellation).ConfigureAwait(true));
        document.RootElement.GetProperty("paths").TryGetProperty("/probe", out _).ShouldBeTrue();
        using var scalar = await client.GetAsync(new Uri("/scalar/v1", UriKind.Relative), cancellation).ConfigureAwait(true);
        scalar.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>FastEndpoints discovers and runs request validators before returning a successful endpoint response.</summary>
    /// <returns>The verification task.</returns>
    [Fact]
    public async Task HostValidatesFastEndpointsRequestsAndReturnsProblemDetails()
    {
        using var client = app.CreateClient();
        var cancellation = TestContext.Current.CancellationToken;
        using var invalid = await client.PostAsJsonAsync(new Uri("/validation-probe", UriKind.Relative), new { name = string.Empty }, cancellation).ConfigureAwait(true);
        invalid.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        invalid.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        using var problem = JsonDocument.Parse(await invalid.Content.ReadAsStringAsync(cancellation).ConfigureAwait(true));
        problem.RootElement.GetProperty("status").GetInt32().ShouldBe((int)HttpStatusCode.BadRequest);
        problem.RootElement.GetProperty("errors").GetArrayLength().ShouldBeGreaterThan(0);
        problem.RootElement.GetRawText().ShouldContain("Name is required.", Case.Sensitive);

        using var valid = await client.PostAsJsonAsync(new Uri("/validation-probe", UriKind.Relative), new { name = "Teck" }, cancellation).ConfigureAwait(true);
        valid.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var response = JsonDocument.Parse(await valid.Content.ReadAsStringAsync(cancellation).ConfigureAwait(true));
        response.RootElement.GetProperty("name").GetString().ShouldBe("Teck");
    }
}

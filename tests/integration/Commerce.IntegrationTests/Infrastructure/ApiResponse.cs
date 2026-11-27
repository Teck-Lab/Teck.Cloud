using System.Net;
using System.Text.Json.Nodes;

namespace Commerce.IntegrationTests.Infrastructure;

/// <summary>One HTTP response with its raw body for contract assertions.</summary>
/// <param name="Status">The status code.</param>
/// <param name="Body">The response body.</param>
internal sealed record ApiResponse(HttpStatusCode Status, string Body)
{
    internal JsonNode Json() => JsonNode.Parse(Body) ?? throw new InvalidOperationException("The response has no JSON body.");

    internal string? Code() => Body.Length == 0 ? null : Json().AsObject()["code"]?.GetValue<string>();

    internal bool IsProblem() => Json().AsObject()["code"] is not null;
}

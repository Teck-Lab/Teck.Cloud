using System.Text.Json.Serialization.Metadata;
using SharedKernel.Core.MultiTenancy;
using Teck.Cloud.Commerce.Gateway.Errors;
using Teck.Cloud.Commerce.Gateway.Http;

namespace Teck.Cloud.Commerce.CustomerGateway.Host.Endpoints;

internal static class CustomerEdge
{
    internal static string Tenant(OperationContext operation) =>
        operation.Scope.TenantId ?? throw new InvalidOperationException("The customer operation scope is not initialized.");

    internal static Task WriteAsync<T>(HttpContext http, GatewayResult<T> result, JsonTypeInfo<T> json, int statusCode = StatusCodes.Status200OK)
        where T : class
    {
        var response = result.IsSuccess
            ? CommerceHttpResults.Success(result.Value!, json, statusCode)
            : CommerceHttpResults.Failure(http, result, json);
        return response.ExecuteAsync(http);
    }

    internal static Task InvalidRequestAsync(HttpContext http) =>
        CommerceHttpResults.InvalidRequest(http).ExecuteAsync(http);
}

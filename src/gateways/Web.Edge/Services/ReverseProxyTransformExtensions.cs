using Microsoft.AspNetCore.Authentication;
using Yarp.ReverseProxy.Transforms;

namespace Web.Edge.Services;

internal static class ReverseProxyTransformExtensions
{
    public static IReverseProxyBuilder AddEdgeGatewayTransforms(
        this IReverseProxyBuilder reverseProxyBuilder,
        EdgeTenantOptions tenantOptions)
    {
        return reverseProxyBuilder.AddTransforms(builderContext =>
        {
            string? clusterId = builderContext.Route?.ClusterId;
            string? routeId = builderContext.Route?.RouteId;
            bool isPublicRoute = EdgeGatewayHelpers.IsAnonymousRoute(builderContext.Route);
            bool isOpenApiRoute = routeId?.Contains("openapi", StringComparison.OrdinalIgnoreCase) == true;
            bool shouldExchangeForRoute = !isOpenApiRoute && !isPublicRoute && !string.IsNullOrWhiteSpace(clusterId);

            builderContext.AddRequestTransform(async transformContext =>
            {
                HttpContext httpContext = transformContext.HttpContext;

                if (EdgeGatewayHelpers.TryGetNonEmptyHeader(httpContext.Request.Headers, tenantOptions.TenantIdHeaderName, out string inboundTenantId))
                {
                    transformContext.ProxyRequest.Headers.Remove(tenantOptions.TenantIdHeaderName);
                    transformContext.ProxyRequest.Headers.TryAddWithoutValidation(tenantOptions.TenantIdHeaderName, inboundTenantId);
                }

                if (EdgeGatewayHelpers.TryGetNonEmptyHeader(
                    httpContext.Request.Headers,
                    EdgeGatewayHelpers.TenantDbStrategyHeaderName,
                    out string tenantDbStrategy))
                {
                    transformContext.ProxyRequest.Headers.Remove(EdgeGatewayHelpers.TenantDbStrategyHeaderName);
                    transformContext.ProxyRequest.Headers.TryAddWithoutValidation(EdgeGatewayHelpers.TenantDbStrategyHeaderName, tenantDbStrategy);
                }

                if (httpContext.Items.TryGetValue(EdgeGatewayHelpers.ExchangedAccessTokenItemKey, out object? exchangedTokenObj) &&
                    exchangedTokenObj is string exchangedToken &&
                    !string.IsNullOrWhiteSpace(exchangedToken))
                {
                    transformContext.ProxyRequest.Headers.Authorization = new("Bearer", exchangedToken);
                    return;
                }

                string? token = EdgeGatewayHelpers.ExtractBearerToken(httpContext.Request.Headers.Authorization.ToString())
                    ?? await httpContext.GetTokenAsync("access_token");

                string? exchangeAudience = shouldExchangeForRoute ? clusterId : null;

                if (string.IsNullOrWhiteSpace(token))
                {
                    return;
                }

                if (string.IsNullOrWhiteSpace(exchangeAudience))
                {
                    transformContext.ProxyRequest.Headers.Authorization = new("Bearer", token);
                    return;
                }

                transformContext.ProxyRequest.Headers.Authorization = new("Bearer", token);
            });
        });
    }
}

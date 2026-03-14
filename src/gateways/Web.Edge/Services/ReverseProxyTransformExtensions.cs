// <copyright file="ReverseProxyTransformExtensions.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

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

                if (string.IsNullOrWhiteSpace(token))
                {
                    return;
                }

                transformContext.ProxyRequest.Headers.Authorization = new("Bearer", token);
            });
        });
    }
}

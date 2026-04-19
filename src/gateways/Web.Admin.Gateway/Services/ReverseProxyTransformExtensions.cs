// <copyright file="ReverseProxyTransformExtensions.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Microsoft.AspNetCore.Authentication;
using Yarp.ReverseProxy.Transforms;

namespace Web.Admin.Gateway.Services;

internal static class ReverseProxyTransformExtensions
{
    public static IReverseProxyBuilder AddAdminGatewayTransforms(
        this IReverseProxyBuilder reverseProxyBuilder,
        AdminGatewayOptions adminOptions)
    {
        return reverseProxyBuilder.AddTransforms(builderContext =>
        {
            builderContext.AddRequestTransform(async transformContext =>
            {
                HttpContext httpContext = transformContext.HttpContext;

                // Forward the bearer token to downstream services unchanged.
                // Token exchange to service-specific audiences happens via Keycloak
                // token exchange in the downstream service if needed.
                string? token = null;

                if (httpContext.Request.Headers.TryGetValue("Authorization", out var authHeader))
                {
                    string authValue = authHeader.ToString();
                    if (authValue.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                    {
                        token = authValue["Bearer ".Length..].Trim();
                    }
                }

                token ??= await httpContext.GetTokenAsync("access_token");

                if (!string.IsNullOrWhiteSpace(token))
                {
                    transformContext.ProxyRequest.Headers.Authorization = new("Bearer", token);
                }
            });
        });
    }
}

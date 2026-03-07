using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Model;

namespace Web.Edge.Services;

internal static class EdgeGatewayHelpers
{
    public const string ExchangedAccessTokenItemKey = "Edge:ExchangedAccessToken";
    public const string ResolvedTenantIdItemKey = "ResolvedTenantId";
    public const string TenantDbStrategyHeaderName = "X-Tenant-DbStrategy";
    public const string EdgeAccessPolicyMetadataKey = "EdgeAccessPolicy";
    public const string EdgeTenantPolicyMetadataKey = "EdgeTenantPolicy";
    public const string EmployeeOnlyPolicyValue = "EmployeeOnly";
    public const string AdminOnlyPolicyValue = "AdminOnly";
    public const string PublicPolicyValue = "Public";
    public const string AuthenticatedPolicyValue = "Authenticated";
    public const string TenantUserPolicyValue = "TenantUser";
    public const string TenantPolicyNoneValue = "None";
    public const string TenantPolicyRequiredValue = "Required";

    public static RouteConfig? ResolveRouteConfig(HttpContext context)
    {
        Endpoint? endpoint = context.GetEndpoint();
        IReverseProxyFeature? reverseProxyFeature = context.Features.Get<IReverseProxyFeature>();

        return reverseProxyFeature?.Route?.Config
            ?? endpoint?.Metadata.GetMetadata<RouteModel>()?.Config
            ?? endpoint?.Metadata.GetMetadata<RouteConfig>();
    }

    public static bool IsAnonymousRoute(RouteConfig? routeConfig)
    {
        if (routeConfig is null)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(routeConfig.AuthorizationPolicy))
        {
            return false;
        }

        return string.Equals(routeConfig.AuthorizationPolicy, "anonymous", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsEmployeeOnlyRoute(RouteConfig? routeConfig, PathString requestPath, string adminPathSegment)
    {
        if (TryGetRouteMetadataValue(routeConfig, EdgeAccessPolicyMetadataKey, out string accessPolicy))
        {
            if (string.Equals(accessPolicy, EmployeeOnlyPolicyValue, StringComparison.OrdinalIgnoreCase)
                || string.Equals(accessPolicy, AdminOnlyPolicyValue, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        if (string.IsNullOrWhiteSpace(adminPathSegment))
        {
            return false;
        }

        return HasPathSegment(requestPath, adminPathSegment);
    }

    public static bool ShouldSkipTenantResolution(RouteConfig? routeConfig, PathString requestPath, string adminPathSegment)
    {
        if (TryGetRouteMetadataValue(routeConfig, EdgeTenantPolicyMetadataKey, out string tenantPolicy))
        {
            if (string.Equals(tenantPolicy, TenantPolicyNoneValue, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (string.Equals(tenantPolicy, TenantPolicyRequiredValue, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return IsEmployeeOnlyRoute(routeConfig, requestPath, adminPathSegment);
    }

    private static bool HasPathSegment(PathString requestPath, string segment)
    {
        string? path = requestPath.Value;
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        string trimmedSegment = segment.Trim().Trim('/');
        if (string.IsNullOrWhiteSpace(trimmedSegment))
        {
            return false;
        }

        string normalizedPath = path.Trim('/');
        if (string.IsNullOrWhiteSpace(normalizedPath))
        {
            return false;
        }

        string[] segments = normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return segments.Any(pathSegment => string.Equals(pathSegment, trimmedSegment, StringComparison.OrdinalIgnoreCase));
    }

    private static bool TryGetRouteMetadataValue(RouteConfig? routeConfig, string key, out string value)
    {
        value = string.Empty;

        if (routeConfig?.Metadata is null)
        {
            return false;
        }

        if (!routeConfig.Metadata.TryGetValue(key, out string? metadataValue))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(metadataValue))
        {
            return false;
        }

        value = metadataValue.Trim();
        return true;
    }

    public static bool TryGetNonEmptyHeader(IHeaderDictionary headers, string headerName, out string value)
    {
        value = string.Empty;

        if (!headers.TryGetValue(headerName, out var values))
        {
            return false;
        }

        string candidate = values.ToString().Trim();
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        value = candidate;
        return true;
    }

    public static string? ExtractBearerToken(string? authorizationHeader)
    {
        if (string.IsNullOrWhiteSpace(authorizationHeader))
        {
            return null;
        }

        if (!authorizationHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return authorizationHeader["Bearer ".Length..].Trim();
    }
}

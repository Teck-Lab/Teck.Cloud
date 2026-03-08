using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using SharedKernel.Infrastructure.Auth;
using SharedKernel.Infrastructure.MultiTenant;
using Web.Edge.Services;
using Yarp.ReverseProxy.Configuration;

namespace Web.Edge.Middleware;

internal sealed class TenantEnforcementMiddleware
{
    private const string NoTenantContextKey = "edge-no-tenant";

    private readonly RequestDelegate _next;
    private readonly EdgeTenantOptions _tenantOptions;
    private readonly EdgeRouteSecurityOptions _routeSecurityOptions;
    private readonly ITenantTokenContextResolver _tenantTokenContextResolver;
    private readonly IServiceTokenExchangeService _tokenExchangeService;
    private readonly ITenantDatabaseStrategyResolver _tenantDatabaseStrategyResolver;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TenantEnforcementMiddleware> _logger;

    public TenantEnforcementMiddleware(
        RequestDelegate next,
        EdgeTenantOptions tenantOptions,
        EdgeRouteSecurityOptions routeSecurityOptions,
        ITenantTokenContextResolver tenantTokenContextResolver,
        IServiceTokenExchangeService tokenExchangeService,
        ITenantDatabaseStrategyResolver tenantDatabaseStrategyResolver,
        IConfiguration configuration,
        ILogger<TenantEnforcementMiddleware> logger)
    {
        _next = next;
        _tenantOptions = tenantOptions;
        _routeSecurityOptions = routeSecurityOptions;
        _tenantTokenContextResolver = tenantTokenContextResolver;
        _tokenExchangeService = tokenExchangeService;
        _tenantDatabaseStrategyResolver = tenantDatabaseStrategyResolver;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        RouteConfig? routeConfig = EdgeGatewayHelpers.ResolveRouteConfig(context);

        if (routeConfig is null)
        {
            await _next(context);
            return;
        }

        bool isOpenApiRequest = context.Request.Path.StartsWithSegments("/openapi", StringComparison.OrdinalIgnoreCase)
            || context.Request.Path.Value?.Contains("/openapi/", StringComparison.OrdinalIgnoreCase) == true;

        bool isDocsRequest = context.Request.Path.StartsWithSegments("/docs", StringComparison.OrdinalIgnoreCase);
        if (isOpenApiRequest || isDocsRequest)
        {
            await _next(context);
            return;
        }

        if (EdgeGatewayHelpers.ShouldSkipTenantResolution(routeConfig, context.Request.Path, _routeSecurityOptions.AdminPathSegment))
        {
            context.Request.Headers.Remove(_tenantOptions.TenantIdHeaderName);
            context.Request.Headers.Remove(EdgeGatewayHelpers.TenantDbStrategyHeaderName);
            context.Items.Remove(EdgeGatewayHelpers.ResolvedTenantIdItemKey);

            bool exchanged = await ExchangeTokenForRouteAsync(context, routeConfig, NoTenantContextKey);
            if (!exchanged)
            {
                return;
            }

            await _next(context);
            return;
        }

        bool isPublicRoute = EdgeGatewayHelpers.IsAnonymousRoute(routeConfig);
        string? resolvedTenantId;

        if (isPublicRoute)
        {
            resolvedTenantId = await ResolveTenantIdForPublicRouteAsync(context);
            if (string.IsNullOrWhiteSpace(resolvedTenantId))
            {
                await WriteTenantValidationFailureAsync(
                    context,
                    StatusCodes.Status400BadRequest,
                    "Missing tenant header",
                    $"Provide '{_tenantOptions.TenantIdHeaderName}' header or include tenant claims in bearer token.",
                    "tenant.header.missing");

                return;
            }
        }
        else
        {
            if (context.User?.Identity?.IsAuthenticated != true)
            {
                AuthenticateResult authResult = await context.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme);
                if (!authResult.Succeeded)
                {
                    authResult = await context.AuthenticateAsync("Bearer");
                }

                if (authResult.Succeeded && authResult.Principal is not null)
                {
                    context.User = authResult.Principal;
                }
            }

            if (context.User?.Identity?.IsAuthenticated != true)
            {
                await WriteTenantValidationFailureAsync(
                    context,
                    StatusCodes.Status401Unauthorized,
                    "Authentication required",
                    "An authenticated token is required for non-public routes.",
                    "authorization.required");

                return;
            }

            IReadOnlyList<string> tokenTenantIds = _tenantTokenContextResolver.ResolveTenantIds(
                context.User,
                _tenantOptions.OrganizationClaimName,
                _tenantOptions.TenantIdClaimName);

            if (tokenTenantIds.Count == 0)
            {
                await WriteTenantValidationFailureAsync(
                    context,
                    StatusCodes.Status403Forbidden,
                    "Missing tenant in token",
                    $"Authenticated token must contain '{_tenantOptions.OrganizationClaimName}' or '{_tenantOptions.TenantIdClaimName}'.",
                    "tenant.token.missing");

                return;
            }

            if (EdgeGatewayHelpers.TryGetNonEmptyHeader(context.Request.Headers, _tenantOptions.TenantIdHeaderName, out string requestedTenantId))
            {
                bool tenantAllowed = tokenTenantIds.Contains(requestedTenantId, StringComparer.OrdinalIgnoreCase);
                if (!tenantAllowed)
                {
                    await WriteTenantValidationFailureAsync(
                        context,
                        StatusCodes.Status403Forbidden,
                        "Tenant mismatch",
                        $"Header '{_tenantOptions.TenantIdHeaderName}' is not present in authenticated token organization claims.",
                        "tenant.mismatch");

                    return;
                }

                resolvedTenantId = requestedTenantId;
            }
            else
            {
                resolvedTenantId = tokenTenantIds[0];
            }
        }

        if (string.IsNullOrWhiteSpace(resolvedTenantId))
        {
            await WriteTenantValidationFailureAsync(
                context,
                StatusCodes.Status400BadRequest,
                "Missing tenant",
                "Unable to resolve tenant for request.",
                "tenant.missing");

            return;
        }

        context.Request.Headers[_tenantOptions.TenantIdHeaderName] = resolvedTenantId;
        context.Request.Headers.Remove(EdgeGatewayHelpers.TenantDbStrategyHeaderName);
        context.Items[EdgeGatewayHelpers.ResolvedTenantIdItemKey] = resolvedTenantId;

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Tenant header applied in Edge. HeaderName={HeaderName}; HeaderValue={HeaderValue}; Path={Path}; TraceId={TraceId}",
                _tenantOptions.TenantIdHeaderName,
                resolvedTenantId,
                context.Request.Path,
                context.TraceIdentifier);
        }

        (bool lookupSuccess, string? dbStrategy, int? lookupStatusCode, string? errorCode, string? errorDetail) = await ResolveTenantDatabaseStrategyAsync(
            resolvedTenantId,
            routeConfig.ClusterId,
            context.RequestAborted);

        if (!lookupSuccess)
        {
            await WriteTenantValidationFailureAsync(
                context,
                lookupStatusCode ?? StatusCodes.Status503ServiceUnavailable,
                "Tenant lookup failed",
                errorDetail ?? "Unable to resolve tenant database strategy.",
                errorCode ?? "tenant.lookup.failed");

            return;
        }

        context.Request.Headers[EdgeGatewayHelpers.TenantDbStrategyHeaderName] = dbStrategy;

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Tenant DB strategy header applied in Edge. HeaderName={HeaderName}; HeaderValue={HeaderValue}; Path={Path}; TraceId={TraceId}",
                EdgeGatewayHelpers.TenantDbStrategyHeaderName,
                dbStrategy,
                context.Request.Path,
                context.TraceIdentifier);
        }

        bool exchangeSucceeded = await ExchangeTokenForRouteAsync(context, routeConfig, resolvedTenantId);
        if (!exchangeSucceeded)
        {
            return;
        }

        await _next(context);
    }

    private async Task<bool> ExchangeTokenForRouteAsync(HttpContext context, RouteConfig routeConfig, string contextKey)
    {
        string? exchangeAudience = ResolveExchangeAudience(routeConfig);
        if (string.IsNullOrWhiteSpace(exchangeAudience))
        {
            return true;
        }

        string? inboundToken = EdgeGatewayHelpers.ExtractBearerToken(context.Request.Headers.Authorization.ToString())
            ?? await context.GetTokenAsync("access_token");

        if (string.IsNullOrWhiteSpace(inboundToken))
        {
            return true;
        }

        try
        {
            ServiceTokenResult exchanged = await _tokenExchangeService.ExchangeTokenAsync(
                inboundToken,
                exchangeAudience,
                contextKey,
                cancellationToken: context.RequestAborted);

            context.Items[EdgeGatewayHelpers.ExchangedAccessTokenItemKey] = exchanged.AccessToken;
            return true;
        }
        catch (TokenExchangeException exception) when (exception.IsAuthFailure)
        {
            int statusCode = exception.StatusCode is StatusCodes.Status401Unauthorized or StatusCodes.Status403Forbidden
                ? exception.StatusCode
                : StatusCodes.Status401Unauthorized;

            bool expiredOrInvalidToken = statusCode == StatusCodes.Status401Unauthorized
                && IsExpiredOrInvalidTokenDescription(exception.Description);

            string detail;
            if (expiredOrInvalidToken)
            {
                detail = "Bearer token expired or invalid. Re-authenticate and try again.";
            }
            else if (string.IsNullOrWhiteSpace(exception.Description))
            {
                detail = "Unable to exchange token for downstream service access.";
            }
            else
            {
                detail = exception.Description;
            }

            string errorCode = expiredOrInvalidToken
                ? "authorization.token.expired"
                : "authorization.token_exchange_denied";

            string title = statusCode == StatusCodes.Status401Unauthorized
                ? "Unauthorized"
                : "Forbidden";

            await WriteTenantValidationFailureAsync(
                context,
                statusCode,
                title,
                detail,
                errorCode);

            return false;
        }
    }

    private string? ResolveExchangeAudience(RouteConfig routeConfig)
    {
        string? clusterId = routeConfig.ClusterId;
        if (string.IsNullOrWhiteSpace(clusterId))
        {
            return null;
        }

        IConfigurationSection destinations = _configuration.GetSection($"ReverseProxy:Clusters:{clusterId}:Destinations");
        foreach (IConfigurationSection destination in destinations.GetChildren())
        {
            string? accessTokenClientName = destination["AccessTokenClientName"];
            if (!string.IsNullOrWhiteSpace(accessTokenClientName))
            {
                return accessTokenClientName.Trim();
            }
        }

        return clusterId;
    }

    private async Task<string?> ResolveTenantIdForPublicRouteAsync(HttpContext context)
    {
        if (EdgeGatewayHelpers.TryGetNonEmptyHeader(context.Request.Headers, _tenantOptions.TenantIdHeaderName, out string tenantIdFromHeader))
        {
            return tenantIdFromHeader;
        }

        if (context.User?.Identity?.IsAuthenticated != true)
        {
            AuthenticateResult authResult = await context.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme);
            if (!authResult.Succeeded)
            {
                authResult = await context.AuthenticateAsync("Bearer");
            }

            if (authResult.Succeeded && authResult.Principal is not null)
            {
                context.User = authResult.Principal;
            }
        }

        if (context.User?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        IReadOnlyList<string> tokenTenantIds = _tenantTokenContextResolver.ResolveTenantIds(
            context.User,
            _tenantOptions.OrganizationClaimName,
            _tenantOptions.TenantIdClaimName);

        return tokenTenantIds.Count == 0 ? null : tokenTenantIds[0];
    }

    private async Task<(bool Success, string? DatabaseStrategy, int? StatusCode, string? ErrorCode, string? ErrorDetail)> ResolveTenantDatabaseStrategyAsync(
        string tenantId,
        string? serviceName,
        CancellationToken cancellationToken)
    {
        TenantDatabaseStrategyLookupResult result = await _tenantDatabaseStrategyResolver
            .ResolveAsync(tenantId, serviceName, cancellationToken)
            .ConfigureAwait(false);

        if (!result.Success)
        {
            _logger.LogWarning("Tenant lookup failed for tenant {TenantId} with error code {ErrorCode}", tenantId, result.ErrorCode);
        }

        return (result.Success, result.DatabaseStrategy, result.StatusCode, result.ErrorCode, result.ErrorDetail);
    }

    private static async Task WriteTenantValidationFailureAsync(
        HttpContext context,
        int statusCode,
        string title,
        string detail,
        string errorCode)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        string traceId = context.TraceIdentifier;

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";

        var problemDetails = new ProblemDetails
        {
            Type = statusCode is StatusCodes.Status401Unauthorized or StatusCodes.Status403Forbidden
                ? "https://tools.ietf.org/html/rfc7235"
                : "https://tools.ietf.org/html/rfc7231#section-6.5.1",
            Title = title,
            Status = statusCode,
            Detail = detail,
            Instance = context.Request.Path,
        };

        problemDetails.Extensions["traceId"] = traceId;
        problemDetails.Extensions["errors"] = new[]
        {
            new { name = errorCode, reason = detail },
        };

        await context.Response.WriteAsJsonAsync(problemDetails);
    }

    private static bool IsExpiredOrInvalidTokenDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return false;
        }

        return description.Contains("expired", StringComparison.OrdinalIgnoreCase)
            || description.Contains("invalid token", StringComparison.OrdinalIgnoreCase)
            || description.Contains("invalid bearer token", StringComparison.OrdinalIgnoreCase)
            || string.Equals(description.Trim(), "Invalid token", StringComparison.OrdinalIgnoreCase);
    }
}

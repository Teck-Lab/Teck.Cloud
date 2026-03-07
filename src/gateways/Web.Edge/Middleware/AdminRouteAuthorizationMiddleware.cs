using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Web.Edge.Services;

namespace Web.Edge.Middleware;

internal sealed class AdminRouteAuthorizationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly EdgeRouteSecurityOptions _securityOptions;

    public AdminRouteAuthorizationMiddleware(
        RequestDelegate next,
        EdgeRouteSecurityOptions securityOptions)
    {
        _next = next;
        _securityOptions = securityOptions;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!_securityOptions.Enabled)
        {
            await _next(context);
            return;
        }

        var routeConfig = EdgeGatewayHelpers.ResolveRouteConfig(context);
        if (routeConfig is null)
        {
            await _next(context);
            return;
        }

        if (!EdgeGatewayHelpers.IsEmployeeOnlyRoute(routeConfig, context.Request.Path, _securityOptions.AdminPathSegment))
        {
            await _next(context);
            return;
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
            await WriteProblemAsync(
                context,
                StatusCodes.Status401Unauthorized,
                "Authentication required",
                "An authenticated bearer token is required for employee-only routes.",
                "authorization.required");

            return;
        }

        if (!HasRequiredRole(context.User, _securityOptions.EmployeeRole))
        {
            await WriteProblemAsync(
                context,
                StatusCodes.Status403Forbidden,
                "Forbidden",
            $"Missing required role '{_securityOptions.EmployeeRole}'.",
                "authorization.role.missing");

            return;
        }

        await _next(context);
    }

    private static bool HasRequiredRole(ClaimsPrincipal principal, string requiredRole)
    {
        if (principal.IsInRole(requiredRole))
        {
            return true;
        }

        if (HasRequiredRoleInRealmAccessClaim(principal, requiredRole))
        {
            return true;
        }

        foreach (Claim claim in principal.Claims)
        {
            bool roleClaim = string.Equals(claim.Type, ClaimTypes.Role, StringComparison.OrdinalIgnoreCase)
                || string.Equals(claim.Type, "role", StringComparison.OrdinalIgnoreCase)
                || string.Equals(claim.Type, "roles", StringComparison.OrdinalIgnoreCase);

            if (!roleClaim)
            {
                continue;
            }

            if (string.Equals(claim.Value, requiredRole, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            string[] tokens = claim.Value.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (tokens.Any(token => string.Equals(token, requiredRole, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasRequiredRoleInRealmAccessClaim(ClaimsPrincipal principal, string requiredRole)
    {
        Claim? realmAccessClaim = principal.Claims.FirstOrDefault(claim =>
            string.Equals(claim.Type, "realm_access", StringComparison.OrdinalIgnoreCase));

        if (realmAccessClaim is null || string.IsNullOrWhiteSpace(realmAccessClaim.Value))
        {
            return false;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(realmAccessClaim.Value);
            if (!document.RootElement.TryGetProperty("roles", out JsonElement rolesElement) ||
                rolesElement.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            foreach (JsonElement roleElement in rolesElement.EnumerateArray())
            {
                string? roleName = roleElement.GetString();
                if (string.Equals(roleName, requiredRole, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static async Task WriteProblemAsync(
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

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";

        var problemDetails = new ProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
            Title = title,
            Status = statusCode,
            Detail = detail,
            Instance = context.Request.Path,
        };

        problemDetails.Extensions["traceId"] = context.TraceIdentifier;
        problemDetails.Extensions["errors"] = new[]
        {
            new { name = errorCode, reason = detail },
        };

        await context.Response.WriteAsJsonAsync(problemDetails);
    }
}

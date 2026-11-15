using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;
using Keycloak.AuthServices.Common.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel.Core.MultiTenancy;

namespace SharedKernel.Infrastructure.MultiTenancy;

/// <summary>Initializes one trusted tenant context after authentication and authorization.</summary>
/// <param name="next">The next middleware.</param>
public sealed class OperationScopeMiddleware(RequestDelegate next)
{
    /// <summary>Applies ownership metadata; ordinary tenant access requires a selected organization.</summary>
    /// <param name="http">The current request.</param>
    /// <param name="operation">Scoped operation identity.</param>
    /// <returns>The pipeline task.</returns>
    /// <exception cref="InvalidOperationException">Tenant endpoints have no registered directory.</exception>
    public async Task InvokeAsync(HttpContext http, OperationContext operation)
    {
        var metadata = http.GetEndpoint()?.Metadata.GetMetadata<OperationScope>();
        if (metadata is null)
        {
            await next(http).ConfigureAwait(false);
            return;
        }

        if (metadata == OperationScope.Global)
        {
            operation.Initialize(DataScope.Global, http.User.FindFirstValue("sub"));
        }
        else
        {
            var tenantId = await ResolveTenantAsync(http, metadata).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(tenantId))
            {
                http.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }

            tenantId = Guid.TryParseExact(tenantId, "D", out var id) ? id.ToString("D") : tenantId;
            var directory = http.RequestServices.GetService<ITenantDirectory>()
                ?? throw new InvalidOperationException("Tenant endpoints require a registered ITenantDirectory.");
            if (!await directory.IsActiveAsync(tenantId, http.RequestAborted).ConfigureAwait(false))
            {
                http.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }

            operation.Initialize(DataScope.ForTenant(tenantId), http.User.FindFirstValue("sub"));
        }

        _ = Activity.Current?.SetTag("tenant.id", operation.Scope.TenantId);
        await next(http).ConfigureAwait(false);
    }

    private static async Task<string?> ResolveTenantAsync(HttpContext http, OperationScope metadata)
    {
        if (http.User.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        if (metadata.AuthorizationPolicy is { } policy)
        {
            var authorization = http.RequestServices.GetRequiredService<IAuthorizationService>();
            var result = await authorization.AuthorizeAsync(http.User, http, policy).ConfigureAwait(false);
            return result.Succeeded && http.Request.RouteValues.TryGetValue(metadata.RouteParameter!, out var value)
                ? value as string
                : null;
        }

        try
        {
            var organizations = http.User.GetOrganizations();
            return organizations.Count == 1 ? organizations[0].Id : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

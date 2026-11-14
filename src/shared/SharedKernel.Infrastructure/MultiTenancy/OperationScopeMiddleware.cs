using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using SharedKernel.Core.MultiTenancy;

namespace SharedKernel.Infrastructure.MultiTenancy;

/// <summary>Initializes operation scope after authentication and authorization, from trusted claims only.</summary>
/// <param name="next">The next middleware.</param>
public sealed class OperationScopeMiddleware(RequestDelegate next)
{
    /// <summary>Applies endpoint ownership metadata; unclassified endpoints remain unscoped.</summary>
    /// <param name="http">The current request.</param>
    /// <param name="operation">Scoped operation identity.</param>
    /// <returns>The pipeline task.</returns>
    /// <exception cref="InvalidOperationException">The tenant scope or its resolver is invalid.</exception>
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
            var ids = http.User.FindAll("tenant_id").Select(claim => claim.Value).Distinct(StringComparer.Ordinal).ToArray();
            if (http.User.Identity?.IsAuthenticated != true || ids.Length != 1 || string.IsNullOrWhiteSpace(ids[0]))
            {
                http.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }

            var directory = http.RequestServices.GetService(typeof(ITenantDirectory)) as ITenantDirectory
                ?? throw new InvalidOperationException("Tenant endpoints require a registered ITenantDirectory.");
            if (!await directory.IsActiveAsync(ids[0], http.RequestAborted).ConfigureAwait(false))
            {
                http.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }

            operation.Initialize(DataScope.ForTenant(ids[0]), http.User.FindFirstValue("sub"));
        }

        _ = Activity.Current?.SetTag("tenant.id", operation.Scope.TenantId);
        await next(http).ConfigureAwait(false);
    }
}

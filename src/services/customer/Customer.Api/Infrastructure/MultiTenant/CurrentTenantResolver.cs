// <copyright file="CurrentTenantResolver.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Finbuckle.MultiTenant.Abstractions;
using SharedKernel.Infrastructure.MultiTenant;

namespace Customer.Api.Infrastructure.MultiTenant;

/// <summary>
/// Resolves the current tenant identifier from the active request context.
/// </summary>
internal static class CurrentTenantResolver
{
    private const string TenantIdClaimName = "tenant_id";

    /// <summary>
    /// Tries to resolve the current tenant identifier.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <param name="tenantContextAccessor">The Finbuckle tenant context accessor.</param>
    /// <param name="tenantId">The resolved tenant identifier when successful.</param>
    /// <returns><see langword="true"/> when a valid tenant identifier is found.</returns>
    public static bool TryResolveTenantId(
        HttpContext httpContext,
        IMultiTenantContextAccessor<TenantDetails>? tenantContextAccessor,
        out Guid tenantId)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        string? tenantIdText = tenantContextAccessor?.MultiTenantContext?.TenantInfo?.Id;
        if (string.IsNullOrWhiteSpace(tenantIdText))
        {
            tenantIdText = httpContext.User.FindFirst(TenantIdClaimName)?.Value;
        }

        return Guid.TryParse(tenantIdText, out tenantId);
    }
}

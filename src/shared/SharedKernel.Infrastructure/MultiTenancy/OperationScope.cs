namespace SharedKernel.Infrastructure.MultiTenancy;

/// <summary>Endpoint metadata selecting an explicit global or tenant data path.</summary>
public sealed class OperationScope
{
    private OperationScope(string? routeParameter = null, string? authorizationPolicy = null)
    {
        RouteParameter = routeParameter;
        AuthorizationPolicy = authorizationPolicy;
    }

    /// <summary>Gets metadata for a global operation. The endpoint must separately require its authorization policy.</summary>
    public static OperationScope Global { get; } = new();

    /// <summary>Gets metadata requiring a single authenticated organization claim and active tenant.</summary>
    public static OperationScope Tenant { get; } = new();

    internal string? RouteParameter { get; }

    internal string? AuthorizationPolicy { get; }

    /// <summary>Selects a tenant from a route only after the caller passes an explicit platform authorization policy.</summary>
    /// <param name="routeParameter">The endpoint's tenant ID route parameter.</param>
    /// <param name="authorizationPolicy">A registered policy granting platform access across organization memberships.</param>
    /// <returns>Metadata for an authorized operation within one active tenant.</returns>
    public static OperationScope AdministrativeTenant(string routeParameter, string authorizationPolicy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(routeParameter);
        ArgumentException.ThrowIfNullOrWhiteSpace(authorizationPolicy);
        return new(routeParameter, authorizationPolicy);
    }
}

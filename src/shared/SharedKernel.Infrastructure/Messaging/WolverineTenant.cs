namespace SharedKernel.Infrastructure.Messaging;

/// <summary>Normalizes Wolverine's non-tenanted envelope metadata, never an application tenant identity.</summary>
internal static class WolverineTenant
{
    internal static bool IsUnspecified(string? tenantId) => string.IsNullOrEmpty(tenantId)
        || string.Equals(tenantId, "*DEFAULT*", StringComparison.Ordinal);
}

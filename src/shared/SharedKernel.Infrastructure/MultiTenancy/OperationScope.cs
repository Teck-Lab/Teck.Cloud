namespace SharedKernel.Infrastructure.MultiTenancy;

/// <summary>Endpoint metadata selecting an explicit global or tenant data path.</summary>
public sealed class OperationScope
{
    private OperationScope()
    {
    }

    /// <summary>Gets metadata for an explicitly authorized global operation.</summary>
    public static OperationScope Global { get; } = new();

    /// <summary>Gets metadata requiring a single authenticated tenant claim and active tenant.</summary>
    public static OperationScope Tenant { get; } = new();
}

namespace SharedKernel.Core.MultiTenancy;

/// <summary>A message with explicit tenant ownership; transport access must authenticate the publisher.</summary>
public interface ITenantMessage
{
    /// <summary>Gets the authorized tenant identifier.</summary>
    string TenantId { get; }
}

using SharedKernel.Core.Domain;

namespace Identity.TestHost;

/// <summary>Fixture record with overlapping keys across tenants.</summary>
public sealed class ProbeRecord : ITenantScoped
{
    /// <summary>Gets or sets the per-tenant key.</summary>
    public string Key { get; set; } = string.Empty;

    /// <inheritdoc/>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>Gets or sets the user-observable value.</summary>
    public string Value { get; set; } = string.Empty;
}

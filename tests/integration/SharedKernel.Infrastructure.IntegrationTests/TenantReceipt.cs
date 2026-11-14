using SharedKernel.Core.Domain;

namespace SharedKernel.Infrastructure.IntegrationTests;

/// <summary>Committed proof that the broker consumer retained the tenant.</summary>
public sealed class TenantReceipt : IReadModel<Guid>, ITenantScoped
{
    /// <inheritdoc/>
    public Guid Id { get; set; }

    /// <inheritdoc/>
    public string TenantId { get; set; } = string.Empty;
}

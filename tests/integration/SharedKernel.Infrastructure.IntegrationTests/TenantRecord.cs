using SharedKernel.Core.Domain;

namespace SharedKernel.Infrastructure.IntegrationTests;

/// <summary>A tenant-owned test aggregate.</summary>
public sealed class TenantRecord : BaseEntity<Guid>, ITenantScoped, IAggregateRoot
{
    /// <summary>Initializes a new instance of the <see cref="TenantRecord"/> class.</summary>
    /// <param name="id">Record identity.</param>
    /// <param name="tenantId">Validated tenant.</param>
    public TenantRecord(Guid id, string tenantId)
    {
        Id = id;
        TenantId = tenantId;
        AddDomainEvent(new TenantRecordCreated { RecordId = id, TenantId = tenantId });
    }

    private TenantRecord()
    {
    }

    /// <inheritdoc/>
    public string TenantId { get; set; } = string.Empty;
}

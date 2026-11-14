using SharedKernel.Core.Events;
using SharedKernel.Core.MultiTenancy;

namespace SharedKernel.Infrastructure.IntegrationTests;

/// <summary>Tests tenant identity across durable local delivery.</summary>
public sealed class TenantRecordCreated : DomainEvent, ITenantMessage
{
    /// <summary>Gets the record identity.</summary>
    public Guid RecordId { get; init; }

    /// <inheritdoc/>
    public required string TenantId { get; init; }
}

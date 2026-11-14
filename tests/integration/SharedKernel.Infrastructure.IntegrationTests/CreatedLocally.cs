using SharedKernel.Core.Events;
using SharedKernel.Core.MultiTenancy;

namespace SharedKernel.Infrastructure.IntegrationTests;

/// <summary>Local domain event.</summary>
public sealed class CreatedLocally : DomainEvent, IGlobalMessage
{
    /// <summary>Gets or sets the record identity.</summary>
    public Guid RecordId { get; set; }
}

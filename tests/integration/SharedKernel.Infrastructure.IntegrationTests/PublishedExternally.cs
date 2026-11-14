using SharedKernel.Core.Events;
using SharedKernel.Core.MultiTenancy;

namespace SharedKernel.Infrastructure.IntegrationTests;

/// <summary>Separate external contract.</summary>
public sealed class PublishedExternally : IntegrationEvent, IGlobalMessage
{
    /// <summary>Gets or sets the record identity.</summary>
    public Guid RecordId { get; set; }
}

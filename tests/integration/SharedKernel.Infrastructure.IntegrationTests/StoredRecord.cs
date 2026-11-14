using SharedKernel.Core.Domain;

namespace SharedKernel.Infrastructure.IntegrationTests;

/// <summary>Test write aggregate.</summary>
public sealed class StoredRecord : BaseEntity<Guid>, IAggregateRoot
{
    /// <summary>Initializes a new instance of the <see cref="StoredRecord"/> class.</summary>
    /// <param name="id">Record identity.</param>
    public StoredRecord(Guid id)
    {
        Id = id;
    }

    private StoredRecord()
    {
    }
}

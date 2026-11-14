namespace SharedKernel.Core.Events;

/// <summary>
/// The domain event.
/// </summary>
public abstract class DomainEvent : IDomainEvent
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DomainEvent"/> class.
    /// </summary>
    protected DomainEvent()
    {
        Id = DefaultIdType.NewGuid();
        CreatedOn = DateTime.UtcNow;
        MetaData = new Dictionary<string, string>(StringComparer.Ordinal);
    }

    /// <summary>
    /// Gets the id.
    /// </summary>
    public DefaultIdType Id { get; init; }

    /// <summary>
    /// Gets the creation date.
    /// </summary>
    public DateTime CreatedOn { get; init; }

    /// <summary>
    /// Gets the meta data.
    /// </summary>
    public IDictionary<string, string> MetaData { get; init; }
}

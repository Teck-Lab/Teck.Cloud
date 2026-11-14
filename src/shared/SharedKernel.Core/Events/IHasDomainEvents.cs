namespace SharedKernel.Core.Events;

/// <summary>Exposes pending local events to the Wolverine EF collector.</summary>
public interface IHasDomainEvents
{
    /// <summary>Gets pending events in occurrence order.</summary>
    IReadOnlyList<IDomainEvent> DomainEvents { get; }

    /// <summary>Drains pending events once.</summary>
    /// <returns>The pending events.</returns>
    IDomainEvent[] ClearDomainEvents();
}

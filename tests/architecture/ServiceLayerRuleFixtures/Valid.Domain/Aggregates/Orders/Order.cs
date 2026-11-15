using SharedKernel.Core.Domain;

namespace ArchitectureRuleFixtures.Valid.Domain.Aggregates.Orders;

/// <summary>The aggregate root in the positive service-layer fixture.</summary>
public sealed class Order : BaseEntity, IAggregateRoot
{
    /// <summary>Gets a value indicating whether the fixture order is closed.</summary>
    public bool IsClosed { get; private set; }

    /// <summary>Closes the fixture order.</summary>
    public void Close() => IsClosed = true;
}

using ArchitectureRuleFixtures.Valid.Application.Sales.Features.CreateOrder.V1;
using ArchitectureRuleFixtures.Valid.Host;
using ArchitectureRuleFixtures.Valid.Infrastructure.Database;
using SharedKernel.Core.Domain;

namespace ArchitectureRuleFixtures.Invalid.Domain.Entities;

/// <summary>An intentionally misplaced aggregate that also demonstrates a reverse layer reference.</summary>
public sealed class Order : BaseEntity, IAggregateRoot
{
    /// <summary>Gets an application request injected to create a prohibited Domain dependency.</summary>
    public CreateOrderCommand? Request { get; }

    /// <summary>Gets an adapter injected to create a prohibited outer-layer dependency.</summary>
    public OrderStore? Adapter { get; }

    /// <summary>Gets a host type injected to create a prohibited transport dependency.</summary>
    public ServiceComposition? Host { get; }
}

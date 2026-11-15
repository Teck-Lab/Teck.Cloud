using SharedKernel.Core.Domain;

namespace ArchitectureRuleFixtures.Valid.Application.Sales.ReadModels;

/// <summary>Query projection mapped by the positive Infrastructure fixture.</summary>
public sealed class GetOrderByIdReadModel : ReadModelBase<Guid>
{
    /// <summary>Gets or sets the order status projection.</summary>
    public string? Status { get; set; }
}

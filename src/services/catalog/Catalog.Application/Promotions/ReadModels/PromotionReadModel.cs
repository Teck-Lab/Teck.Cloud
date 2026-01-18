using SharedKernel.Core.Domain;

namespace Catalog.Application.Promotions.ReadModels;

/// <summary>
/// Read model for Promotion entities, optimized for queries.
/// </summary>
public class PromotionReadModel : ReadModelBase<Guid>
{
    /// <summary>
    /// Gets or sets the name of the promotion.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of the promotion.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the discount percentage.
    /// </summary>
    public decimal DiscountPercentage { get; set; }

    /// <summary>
    /// Gets or sets the start date of the promotion.
    /// </summary>
    public DateTimeOffset StartDate { get; set; }

    /// <summary>
    /// Gets or sets the end date of the promotion.
    /// </summary>
    public DateTimeOffset EndDate { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the promotion is active.
    /// </summary>
    public bool IsActive { get; set; }
}

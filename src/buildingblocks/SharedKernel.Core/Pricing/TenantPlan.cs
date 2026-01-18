using Ardalis.SmartEnum;

namespace SharedKernel.Core.Pricing;

/// <summary>
/// Represents the available tenant plans.
/// </summary>
public sealed class TenantPlan : SmartEnum<TenantPlan>
{
    /// <summary>
    /// Gets the database strategy associated with this tenant plan.
    /// </summary>
    public DatabaseStrategy DatabaseStrategy { get; }

    /// <summary>
    /// Gets the base price for this tenant plan.
    /// </summary>
    public decimal BasePrice { get; }

    /// <summary>
    /// Gets a description of the plan.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// No plan assigned.
    /// </summary>
    public static readonly TenantPlan None = new(nameof(None), 0, DatabaseStrategy.None, 0m, "No plan assigned.");

    /// <summary>
    /// Shared tenant plan.
    /// </summary>
    public static readonly TenantPlan Shared = new(nameof(Shared), 1, DatabaseStrategy.Shared, 29.99m, "Shared plan for entry-level tenants.");

    /// <summary>
    /// Premium tenant plan.
    /// </summary>
    public static readonly TenantPlan Premium = new(nameof(Premium), 2, DatabaseStrategy.Dedicated, 99.99m, "Premium plan with dedicated resources.");

    /// <summary>
    /// Business/Professional tenant plan (between Premium and Enterprise).
    /// </summary>
    public static readonly TenantPlan Business = new(nameof(Business), 3, DatabaseStrategy.Dedicated, 149.99m, "Business plan for professional tenants.");

    /// <summary>
    /// Enterprise tenant plan.
    /// </summary>
    public static readonly TenantPlan Enterprise = new(nameof(Enterprise), 4, DatabaseStrategy.External, 299.99m, "Enterprise plan with external database.");

    /// <summary>
    /// Trial tenant plan.
    /// </summary>
    public static readonly TenantPlan Trial = new(nameof(Trial), 5, DatabaseStrategy.Shared, 0m, "Trial plan for evaluation.");

    /// <summary>
    /// Inactive or archived tenant plan.
    /// </summary>
    public static readonly TenantPlan InactiveArchived = new(nameof(InactiveArchived), 6, DatabaseStrategy.Shared, 0m, "Inactive or archived plan.");

    private TenantPlan(string name, int value, DatabaseStrategy databaseStrategy, decimal basePrice, string description) : base(name, value)
    {
        DatabaseStrategy = databaseStrategy;
        BasePrice = basePrice;
        Description = description;
    }

    /// <summary>
    /// Gets the database strategy associated with a given tenant plan.
    /// </summary>
    /// <param name="plan">The tenant plan.</param>
    /// <returns>The corresponding <see cref="DatabaseStrategy"/>.</returns>
    public static DatabaseStrategy GetDatabaseStrategyFromPlan(TenantPlan plan)
        => plan?.DatabaseStrategy ?? DatabaseStrategy.Shared;

    /// <summary>
    /// Gets the base price for a given tenant plan.
    /// </summary>
    /// <param name="plan">The tenant plan.</param>
    /// <returns>The base price as a decimal.</returns>
    public static decimal GetBasePriceForPlan(TenantPlan plan)
        => plan?.BasePrice ?? 0.00m;
}

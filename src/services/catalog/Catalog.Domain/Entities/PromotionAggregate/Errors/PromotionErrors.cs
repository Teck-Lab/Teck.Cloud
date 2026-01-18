using ErrorOr;

namespace Catalog.Domain.Entities.PromotionAggregate.Errors;

/// <summary>
/// Provides predefined errors related to the Promotion entity.
/// </summary>
public static class PromotionErrors
{
    /// <summary>
    /// Gets the error indicating that the promotion name cannot be empty.
    /// </summary>
    public static Error EmptyName => Error.Validation(
        code: "Promotion.EmptyName",
        description: "Promotion name cannot be empty.");

    /// <summary>
    /// Gets the error indicating that the promotion discount must be greater than zero.
    /// </summary>
    public static Error InvalidDiscount => Error.Validation(
        code: "Promotion.InvalidDiscount",
        description: "Promotion discount must be greater than zero.");

    /// <summary>
    /// Gets the error indicating that the promotion end date must be after the start date.
    /// </summary>
    public static Error InvalidDateRange => Error.Validation(
        code: "Promotion.InvalidDateRange",
        description: "Promotion date range is invalid: start date must be before end date.");

    /// <summary>
    /// Gets the error indicating that a promotion must have at least one product.
    /// </summary>
    public static Error NoProducts => Error.Validation(
                    code: "Promotion.NoProducts",
                    description: "Promotion must have at least one product.");
}

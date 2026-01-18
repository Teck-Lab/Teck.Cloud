using ErrorOr;

namespace Catalog.Domain.Entities.BrandAggregate.Errors
{
    /// <summary>
    /// The brand.
    /// </summary>
    public static class BrandErrors
    {
        /// <summary>
        /// Gets brand not found error.
        /// </summary>
        public static Error NotFound => Error.NotFound(
            code: "Brand.NotFound",
            description: "The specified brand was not found");

        /// <summary>
        /// Gets brand empty name validation error.
        /// </summary>
        public static Error EmptyName => Error.Validation(
            code: "Brand.EmptyName",
            description: "Brand name cannot be empty.");

        /// <summary>
        /// Gets brand empty description validation error.
        /// </summary>
        public static Error EmptyDescription => Error.Validation(
            code: "Brand.EmptyDescription",
            description: "Brand description cannot be empty.");

        /// <summary>
        /// Gets brand invalid website validation error.
        /// </summary>
        public static Error InvalidWebsite => Error.Validation(
            code: "Brand.InvalidWebsite",
            description: "Brand website must be a valid URL.");

        /// <summary>
        /// Gets brand empty website validation error.
        /// </summary>
        public static Error EmptyWebsite => Error.Validation(
            code: "Brand.EmptyWebsite",
            description: "Brand website cannot be empty.");
    }
}

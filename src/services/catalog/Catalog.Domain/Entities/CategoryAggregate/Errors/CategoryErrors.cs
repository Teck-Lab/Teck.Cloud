using ErrorOr;

namespace Catalog.Domain.Entities.CategoryAggregate.Errors
{
    /// <summary>
    /// Provides error definitions for category-related operations.
    /// </summary>
    public static class CategoryErrors
    {
        /// <summary>
        /// Gets brand not found error.
        /// </summary>
        public static Error NotFound => Error.NotFound(
            code: "Category.NotFound",
            description: "Category not found");

        /// <summary>
        /// Gets category empty name validation error.
        /// </summary>
        public static Error EmptyName => Error.Validation(
            code: "Category.EmptyName",
            description: "Category name cannot be empty.");

        /// <summary>
        /// Gets category empty description validation error.
        /// </summary>
        public static Error EmptyDescription => Error.Validation(
            code: "Category.EmptyDescription",
            description: "Category description cannot be empty.");
    }
}

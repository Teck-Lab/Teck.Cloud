using Catalog.Domain.Entities.CategoryAggregate.Errors;
using Catalog.Domain.Entities.ProductAggregate;
using Catalog.Domain.Entities.PromotionAggregate;
using ErrorOr;
using Finbuckle.MultiTenant;
using SharedKernel.Core.Domain;

namespace Catalog.Domain.Entities.CategoryAggregate
{
    /// <summary>
    /// The category.
    /// </summary>
    [MultiTenant]
    public class Category : BaseEntity, IAggregateRoot
    {
        /// <summary>
        /// Gets the name.
        /// </summary>
        public string? Name { get; private set; }

        /// <summary>
        /// Gets the description.
        /// </summary>
        public string? Description { get; private set; }

        /// <summary>
        /// Gets the products.
        /// </summary>
        public ICollection<Product> Products { get; private set; } = [];

        /// <summary>
        /// Gets the promotions.
        /// </summary>
        public ICollection<Promotion> Promotions { get; private set; } = [];

        /// <summary>
        /// Update category.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="description"></param>
        /// <returns></returns>
        public ErrorOr<Updated> Update(
            string? name,
            string? description)
        {
            var errors = new List<Error>();

            if (name is not null)
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    errors.Add(CategoryErrors.EmptyName);
                }
                else if (!string.Equals(Name, name, StringComparison.Ordinal))
                {
                    Name = name;
                }
            }

            if (description is not null)
            {
                if (string.IsNullOrWhiteSpace(description))
                {
                    errors.Add(CategoryErrors.EmptyDescription);
                }
                else if (!string.Equals(Description, description, StringComparison.Ordinal))
                {
                    Description = description;
                }
            }

            if (errors.Count != 0)
            {
                return errors;
            }

            return Result.Updated;
        }

        /// <summary>
        /// Create category.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="description"></param>
        /// <returns></returns>
        public static ErrorOr<Category> Create(string name, string? description)
        {
            var errors = new List<Error>();

            if (string.IsNullOrWhiteSpace(name))
            {
                errors.Add(CategoryErrors.EmptyName);
            }

            if (string.IsNullOrWhiteSpace(description))
            {
                errors.Add(CategoryErrors.EmptyDescription);
            }

            if (errors.Count != 0)
            {
                return errors;
            }

            var category = new Category
            {
                Name = name,
                Description = description
            };

            return category;
        }
    }
}

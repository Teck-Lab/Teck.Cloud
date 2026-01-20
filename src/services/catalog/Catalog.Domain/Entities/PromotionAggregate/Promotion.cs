using Catalog.Domain.Entities.CategoryAggregate;
using Catalog.Domain.Entities.ProductAggregate;
using Catalog.Domain.Entities.PromotionAggregate.Errors;
using ErrorOr;
using Finbuckle.MultiTenant.Abstractions;
using SharedKernel.Core.Domain;

namespace Catalog.Domain.Entities.PromotionAggregate
{
    /// <summary>
    /// The promotion.
    /// </summary>
    [MultiTenant]
    public class Promotion : BaseEntity, IAggregateRoot
    {
        /// <summary>
        /// Gets the name.
        /// </summary>
        public string Name { get; private set; } = default!;

        /// <summary>
        /// Gets the description.
        /// </summary>
        public string? Description { get; private set; }

        /// <summary>
        /// Gets the valid from.
        /// </summary>
        public DateTimeOffset ValidFrom { get; private set; } = default!;

        /// <summary>
        /// Gets the valid converts to.
        /// </summary>
        public DateTimeOffset ValidTo { get; private set; } = default!;

        /// <summary>
        /// Gets the products.
        /// </summary>
        public ICollection<Product> Products { get; private set; } = [];

        /// <summary>
        /// Gets the categories.
        /// </summary>
        public ICollection<Category> Categories { get; private set; } = [];

        /// <summary>
        /// Update a brand.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="description"></param>
        /// <param name="validFrom"></param>
        /// <param name="validTo"></param>
        /// <param name="products"></param>
        /// <returns></returns>
        public ErrorOr<Updated> Update(
            string? name,
            string? description,
            DateTimeOffset? validFrom,
            DateTimeOffset? validTo,
            ICollection<Product>? products)
        {
            var errors = new List<Error>();

            if (name is not null)
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    errors.Add(PromotionErrors.EmptyName);
                }
                else if (!Name.Equals(name, StringComparison.Ordinal))
                {
                    Name = name;
                }
            }

            if (description is not null && !string.Equals(Description, description, StringComparison.Ordinal))
            {
                Description = description;
            }

            if (validFrom.HasValue && !ValidFrom.Equals(validFrom.Value))
            {
                ValidFrom = validFrom.Value;
            }

            if (validTo.HasValue && !ValidTo.Equals(validTo.Value))
            {
                if (validFrom.HasValue && validTo.Value < validFrom.Value)
                {
                    errors.Add(PromotionErrors.InvalidDateRange);
                }
                else if (!validFrom.HasValue && validTo.Value < ValidFrom)
                {
                    errors.Add(PromotionErrors.InvalidDateRange);
                }
                else
                {
                    ValidTo = validTo.Value;
                }
            }

            if (products is not null)
            {
                Products = products;
            }

            if (errors.Count != 0)
            {
                return errors;
            }

            return Result.Updated;
        }

        /// <summary>
        /// Create a brand.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="description"></param>
        /// <param name="validFrom"></param>
        /// <param name="validTo"></param>
        /// <param name="products"></param>
        /// <returns></returns>
        public static ErrorOr<Promotion> Create(
            string name,
            string? description,
            DateTimeOffset validFrom,
            DateTimeOffset validTo,
            ICollection<Product> products)
        {
            var errors = new List<Error>();

            if (string.IsNullOrWhiteSpace(name))
            {
                errors.Add(PromotionErrors.EmptyName);
            }

            if (validTo < validFrom)
            {
                errors.Add(PromotionErrors.InvalidDateRange);
            }

            if (products == null || products.Count == 0)
            {
                errors.Add(PromotionErrors.NoProducts);
            }

            if (errors.Count != 0)
            {
                return errors;
            }

            Promotion promotion = new()
            {
                Name = name,
                Description = description,
                ValidFrom = validFrom,
                ValidTo = validTo,
                Products = products ?? throw new ArgumentNullException(nameof(products))
            };

            return promotion;
        }
    }
}

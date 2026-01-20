using Catalog.Domain.Entities.ProductAggregate;
using Catalog.Domain.Entities.ProductPriceTypeAggregate.Errors;
using ErrorOr;
using Finbuckle.MultiTenant.Abstractions;
using SharedKernel.Core.Domain;

namespace Catalog.Domain.Entities.ProductPriceTypeAggregate
{
    /// <summary>
    /// The product price type.
    /// </summary>
    [MultiTenant]
    public class ProductPriceType : BaseEntity, IAggregateRoot
    {
        /// <summary>
        /// Gets the product prices.
        /// </summary>
        public ICollection<ProductPrice> ProductPrices { get; private set; } = [];

        /// <summary>
        /// Gets the name.
        /// </summary>
        public string Name { get; private set; } = default!;

        /// <summary>
        /// Gets the index.
        /// </summary>
        public int Priority { get; private set; } = default!;

        /// <summary>
        /// Update Product Price Type.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="priority"></param>
        /// <returns></returns>
        public ErrorOr<Updated> Update(string? name, int? priority)
        {
            var errors = new List<Error>();

            if (name is not null)
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    errors.Add(ProductPriceTypeErrors.EmptyName);
                }
                else if (!Name.Equals(name, StringComparison.Ordinal))
                {
                    Name = name;
                }
            }

            if (priority.HasValue)
            {
                if (priority.Value < 0)
                {
                    errors.Add(ProductPriceTypeErrors.NegativePriority);
                }
                else if (!Priority.Equals(priority.Value))
                {
                    Priority = priority.Value;
                }
            }

            if (errors.Count != 0)
            {
                return errors;
            }

            return Result.Updated;
        }

        /// <summary>
        /// Create Product Price Type.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="priority"></param>
        /// <returns></returns>
        public static ErrorOr<ProductPriceType> Create(string name, int priority)
        {
            var validationResult = ValidateCreation(name, priority);
            if (validationResult.IsError)
            {
                return validationResult.Errors;
            }

            ProductPriceType productPriceType = new()
            {
                Name = name,
                Priority = priority
            };
            return productPriceType;
        }

        private static ErrorOr<Success> ValidateCreation(string name, int priority)
        {
            var errors = new List<Error>();

            if (string.IsNullOrWhiteSpace(name))
            {
                errors.Add(ProductPriceTypeErrors.EmptyName);
            }

            if (priority < 0)
            {
                errors.Add(ProductPriceTypeErrors.NegativePriority);
            }

            return errors.Count != 0 ? errors : Result.Success;
        }
    }
}

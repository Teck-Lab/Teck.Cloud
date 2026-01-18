using System.Text.RegularExpressions;
using Catalog.Domain.Entities.BrandAggregate;
using Catalog.Domain.Entities.CategoryAggregate;
using Catalog.Domain.Entities.ProductAggregate.Errors;
using Catalog.Domain.Entities.ProductAggregate.Events;
using Catalog.Domain.Entities.PromotionAggregate;
using ErrorOr;
using Finbuckle.MultiTenant;
using SharedKernel.Core.Domain;

namespace Catalog.Domain.Entities.ProductAggregate
{
    /// <summary>
    /// The product.
    /// </summary>
    [MultiTenant]
    public partial class Product : BaseEntity, IAggregateRoot
    {
        /// <summary>
        /// Gets the name.
        /// </summary>
        public string Name { get; private set; } = default!;

        /// <summary>
        /// Gets the details.
        /// </summary>
        public string? Description { get; private set; } = default!;

        /// <summary>
        /// Gets the slug.
        /// </summary>
        public string Slug { get; private set; } = default!;

        /// <summary>
        /// Gets a value indicating whether active.
        /// </summary>
        public bool IsActive { get; private set; }

        /// <summary>
        /// Gets the product SKU.
        /// </summary>
        public string ProductSKU { get; private set; } = default!;

        /// <summary>
        /// Gets the GTIN.
        /// </summary>
        public string? GTIN { get; private set; } = default!;

        /// <summary>
        /// Gets the Brand id.
        /// </summary>
        public Guid? BrandId { get; private set; }

        /// <summary>
        /// Gets the brand.
        /// </summary>
        public Brand? Brand { get; }

        /// <summary>
        /// Gets the categories.
        /// </summary>
        public ICollection<Category> Categories { get; private set; } = [];

        /// <summary>
        /// Gets the product prices.
        /// </summary>
        public ICollection<ProductPrice> ProductPrices { get; private set; } = [];

        /// <summary>
        /// Gets the promotions.
        /// </summary>
        public ICollection<Promotion> Promotions { get; private set; } = [];

        /// <summary>
        /// Update a brand.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public ErrorOr<Updated> Update(string? name)
        {
            var errors = new List<Error>();

            if (name is not null)
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    errors.Add(ProductErrors.EmptyName);
                }
                else if (!Name.Equals(name, StringComparison.Ordinal))
                {
                    Name = name;
                    Slug = GetProductSlug(name);
                }
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
        /// <param name="sku"></param>
        /// <param name="gtin"></param>
        /// <param name="categories"></param>
        /// <param name="isActive"></param>
        /// <param name="brandId"></param>
        public static ErrorOr<Product> Create(
            string name,
            string? description,
            string? sku,
            string? gtin,
            ICollection<Category> categories,
            bool isActive,
            Guid? brandId = null)
        {
            var validationResult = ValidateProductCreation(name, description, sku);
            if (validationResult.IsError)
            {
                return validationResult.Errors;
            }

            var product = CreateProductInstance(name, description, sku, gtin, categories, isActive, brandId);
            var domainEvent = new ProductCreatedDomainEvent(product.Id, product.Name);
            product.AddDomainEvent(domainEvent);

            return product;
        }

        private static ErrorOr<Success> ValidateProductCreation(string name, string? description, string? sku)
        {
            var errors = new List<Error>();

            if (string.IsNullOrWhiteSpace(name))
            {
                errors.Add(ProductErrors.EmptyName);
            }

            if (string.IsNullOrWhiteSpace(description))
            {
                errors.Add(ProductErrors.EmptyDescription);
            }

            if (string.IsNullOrWhiteSpace(sku))
            {
                errors.Add(ProductErrors.EmptySKU);
            }

            return errors.Count != 0 ? errors : Result.Success;
        }

        private static Product CreateProductInstance(
            string name,
            string? description,
            string? sku,
            string? gtin,
            ICollection<Category> categories,
            bool isActive,
            Guid? brandId)
        {
            return new Product
            {
                Name = name,
                Description = description,
                ProductSKU = sku!,
                GTIN = gtin,
                Categories = categories ?? new List<Category>(),
                IsActive = isActive,
                BrandId = brandId,
                Slug = name.ToLower(System.Globalization.CultureInfo.InvariantCulture).Replace(" ", "-", StringComparison.Ordinal)
            };
        }

        /// <summary>
        /// Regex for replacing non-alphanumeric characters.
        /// </summary>
        /// <returns>A compiled regex.</returns>
        [GeneratedRegex("[^a-z0-9]+", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
        private static partial Regex NonAlphanumericRegex();

        /// <summary>
        /// Regex for replacing multiple consecutive hyphens.
        /// </summary>
        /// <returns>A compiled regex.</returns>
        [GeneratedRegex("--+", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
        private static partial Regex MultipleHyphensRegex();

        /// <summary>
        /// Get product slug from product name.
        /// </summary>
        /// <param name="name">The product name.</param>
        /// <returns>A URL-friendly slug.</returns>
        private static string GetProductSlug(string name)
        {
            name = name.Trim();
            name = name.ToLower(System.Globalization.CultureInfo.InvariantCulture);
            name = NonAlphanumericRegex().Replace(name, "-");
            name = MultipleHyphensRegex().Replace(name, "-");
            name = name.Trim('-');
            return name;
        }
    }
}

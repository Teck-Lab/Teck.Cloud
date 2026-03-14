// <copyright file="Product.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using System.Text.RegularExpressions;
using Catalog.Domain.Entities.BrandAggregate;
using Catalog.Domain.Entities.CategoryAggregate;
using Catalog.Domain.Entities.ProductAggregate.Errors;
using Catalog.Domain.Entities.ProductAggregate.Events;
using Catalog.Domain.Entities.PromotionAggregate;
using ErrorOr;
using SharedKernel.Core.Domain;

namespace Catalog.Domain.Entities.ProductAggregate
{
    /// <summary>
    /// The product.
    /// </summary>
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
        /// Creates a product.
        /// </summary>
        /// <param name="name">The product name.</param>
        /// <param name="description">The product description.</param>
        /// <param name="sku">The product SKU.</param>
        /// <param name="gtin">The product GTIN.</param>
        /// <param name="categories">The product categories.</param>
        /// <param name="isActive">Whether the product is active.</param>
        /// <param name="brandId">The optional brand identifier.</param>
        /// <returns>The created product, or validation errors.</returns>
        public static ErrorOr<Product> Create(
            string name,
            string? description,
            string? sku,
            string? gtin,
            ICollection<Category> categories,
            bool isActive,
            Guid? brandId = null)
        {
            ArgumentNullException.ThrowIfNull(name);

            var validationResult = ValidateCreation(name, description, sku);
            if (validationResult.IsError)
            {
                return validationResult.Errors;
            }

            var product = new Product
            {
                Name = name,
                Description = description,
                ProductSKU = sku!,
                GTIN = gtin,
                Categories = categories ?? new List<Category>(),
                IsActive = isActive,
                BrandId = brandId,
                Slug = name.ToLower(System.Globalization.CultureInfo.InvariantCulture).Replace(" ", "-", StringComparison.Ordinal),
            };
            AddCreatedDomainEvent(product);

            return product;
        }

        /// <summary>
        /// Updates product name.
        /// </summary>
        /// <param name="name">The new product name.</param>
        /// <returns>An updated result, or validation errors.</returns>
        public ErrorOr<Updated> Update(string? name)
        {
            var errors = new List<Error>();

            this.UpdateName(name, errors);

            if (errors.Count != 0)
            {
                return errors;
            }

            return Result.Updated;
        }

        private static ErrorOr<Success> ValidateCreation(string name, string? description, string? sku)
        {
            var errors = new List<Error>();

            ValidateNameForCreate(name, errors);
            ValidateDescriptionForCreate(description, errors);
            ValidateSkuForCreate(sku, errors);

            return errors.Count != 0 ? errors : Result.Success;
        }

        private static void ValidateNameForCreate(string name, List<Error> errors)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                errors.Add(ProductErrors.EmptyName);
            }
        }

        private static void ValidateDescriptionForCreate(string? description, List<Error> errors)
        {
            if (string.IsNullOrWhiteSpace(description))
            {
                errors.Add(ProductErrors.EmptyDescription);
            }
        }

        private static void ValidateSkuForCreate(string? sku, List<Error> errors)
        {
            if (string.IsNullOrWhiteSpace(sku))
            {
                errors.Add(ProductErrors.EmptySKU);
            }
        }

        private static void AddCreatedDomainEvent(Product product)
        {
            var productCreatedDomainEvent = new ProductCreatedDomainEvent(product.Id, product.Name);
            product.AddDomainEvent(productCreatedDomainEvent);
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
        /// Builds a slug from product name.
        /// </summary>
        /// <param name="name">The product name.</param>
        /// <returns>A URL-friendly slug.</returns>
        private static string BuildSlug(string name)
        {
            name = name.Trim();
            name = name.ToLower(System.Globalization.CultureInfo.InvariantCulture);
            name = NonAlphanumericRegex().Replace(name, "-");
            name = MultipleHyphensRegex().Replace(name, "-");
            name = name.Trim('-');
            return name;
        }

        private void UpdateName(string? name, List<Error> errors)
        {
            if (name is null || this.Name.Equals(name, StringComparison.Ordinal))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                errors.Add(ProductErrors.EmptyName);
                return;
            }

            this.Name = name;
            this.Slug = BuildSlug(name);
        }
    }
}

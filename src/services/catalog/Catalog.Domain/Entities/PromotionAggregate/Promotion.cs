// <copyright file="Promotion.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Catalog.Domain.Entities.CategoryAggregate;
using Catalog.Domain.Entities.ProductAggregate;
using Catalog.Domain.Entities.PromotionAggregate.Errors;
using ErrorOr;
using SharedKernel.Core.Domain;

namespace Catalog.Domain.Entities.PromotionAggregate
{
    /// <summary>
    /// The promotion.
    /// </summary>
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
        /// Creates a promotion.
        /// </summary>
        /// <param name="name">The promotion name.</param>
        /// <param name="description">The optional promotion description.</param>
        /// <param name="validFrom">The start date.</param>
        /// <param name="validTo">The end date.</param>
        /// <param name="products">The products in scope.</param>
        /// <returns>The created promotion, or validation errors.</returns>
        public static ErrorOr<Promotion> Create(
            string name,
            string? description,
            DateTimeOffset validFrom,
            DateTimeOffset validTo,
            ICollection<Product> products)
        {
            var errors = new List<Error>();

            ValidateNameForCreate(name, errors);
            ValidateDateRangeForCreate(validFrom, validTo, errors);
            ValidateProductsForCreate(products, errors);

            if (errors.Count > 0)
            {
                return errors.ToArray();
            }

            return new Promotion
            {
                Name = name,
                Description = description,
                ValidFrom = validFrom,
                ValidTo = validTo,
                Products = products,
            };
        }

        /// <summary>
        /// Updates a promotion.
        /// </summary>
        /// <param name="name">The updated promotion name.</param>
        /// <param name="description">The updated promotion description.</param>
        /// <param name="validFrom">The updated start date.</param>
        /// <param name="validTo">The updated end date.</param>
        /// <param name="products">The updated product list.</param>
        /// <returns>An updated result, or validation errors.</returns>
        public ErrorOr<Updated> Update(
            string? name,
            string? description,
            DateTimeOffset? validFrom,
            DateTimeOffset? validTo,
            ICollection<Product>? products)
        {
            var errors = new List<Error>();

            this.UpdateName(name, errors);
            this.UpdateDescription(description);
            this.UpdateValidFrom(validFrom);
            this.UpdateValidTo(validFrom, validTo, errors);
            this.UpdateProducts(products);

            return errors.Count != 0 ? errors : Result.Updated;
        }

        private static void ValidateNameForCreate(string name, List<Error> errors)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                errors.Add(PromotionErrors.EmptyName);
            }
        }

        private static void ValidateDateRangeForCreate(DateTimeOffset validFrom, DateTimeOffset validTo, List<Error> errors)
        {
            if (validTo < validFrom)
            {
                errors.Add(PromotionErrors.InvalidDateRange);
            }
        }

        private static void ValidateProductsForCreate(ICollection<Product> products, List<Error> errors)
        {
            if (products == null || products.Count == 0)
            {
                errors.Add(PromotionErrors.NoProducts);
            }
        }

        private void UpdateName(string? name, List<Error> errors)
        {
            if (name is null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                errors.Add(PromotionErrors.EmptyName);
                return;
            }

            if (!this.Name.Equals(name, StringComparison.Ordinal))
            {
                this.Name = name;
            }
        }

        private void UpdateDescription(string? description)
        {
            if (description is not null && !string.Equals(this.Description, description, StringComparison.Ordinal))
            {
                this.Description = description;
            }
        }

        private void UpdateValidFrom(DateTimeOffset? validFrom)
        {
            if (validFrom is not null && !this.ValidFrom.Equals(validFrom.Value))
            {
                this.ValidFrom = validFrom.Value;
            }
        }

        private void UpdateValidTo(DateTimeOffset? validFrom, DateTimeOffset? validTo, List<Error> errors)
        {
            if (!this.TryGetValidToForUpdate(validTo, out var validToValue))
            {
                return;
            }

            if (this.IsInvalidDateRange(validFrom, validToValue))
            {
                errors.Add(PromotionErrors.InvalidDateRange);
                return;
            }

            this.ValidTo = validToValue;
        }

        private bool TryGetValidToForUpdate(DateTimeOffset? validTo, out DateTimeOffset validToValue)
        {
            validToValue = default;

            if (validTo is null || this.ValidTo.Equals(validTo.Value))
            {
                return false;
            }

            validToValue = validTo.Value;
            return true;
        }

        private bool IsInvalidDateRange(DateTimeOffset? validFrom, DateTimeOffset validTo)
        {
            if (validFrom is not null)
            {
                return validTo < validFrom.Value;
            }

            return validTo < this.ValidFrom;
        }

        private void UpdateProducts(ICollection<Product>? products)
        {
            if (products is not null)
            {
                this.Products = products;
            }
        }
    }
}

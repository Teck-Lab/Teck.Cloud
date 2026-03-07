// <copyright file="ProductPriceType.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Catalog.Domain.Entities.ProductAggregate;
using Catalog.Domain.Entities.ProductPriceTypeAggregate.Errors;
using ErrorOr;
using SharedKernel.Core.Domain;

namespace Catalog.Domain.Entities.ProductPriceTypeAggregate
{
    /// <summary>
    /// The product price type.
    /// </summary>
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
        /// Creates a product price type.
        /// </summary>
        /// <param name="name">The price type name.</param>
        /// <param name="priority">The priority.</param>
        /// <returns>The created product price type, or validation errors.</returns>
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
                Priority = priority,
            };
            return productPriceType;
        }

        /// <summary>
        /// Updates product price type values.
        /// </summary>
        /// <param name="name">The price type name.</param>
        /// <param name="priority">The priority.</param>
        /// <returns>An updated result, or validation errors.</returns>
        public ErrorOr<Updated> Update(string? name, int? priority)
        {
            var errors = new List<Error>();

            this.UpdateName(name, errors);
            this.UpdatePriority(priority, errors);

            if (errors.Count != 0)
            {
                return errors;
            }

            return Result.Updated;
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

        private void UpdateName(string? name, List<Error> errors)
        {
            if (name is null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                errors.Add(ProductPriceTypeErrors.EmptyName);
                return;
            }

            if (!this.Name.Equals(name, StringComparison.Ordinal))
            {
                this.Name = name;
            }
        }

        private void UpdatePriority(int? priority, List<Error> errors)
        {
            if (priority is not int priorityValue)
            {
                return;
            }

            if (priorityValue < 0)
            {
                errors.Add(ProductPriceTypeErrors.NegativePriority);
                return;
            }

            if (!this.Priority.Equals(priorityValue))
            {
                this.Priority = priorityValue;
            }
        }
    }
}

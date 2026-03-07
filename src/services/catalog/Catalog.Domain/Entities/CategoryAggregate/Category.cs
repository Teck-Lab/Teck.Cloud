// <copyright file="Category.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Catalog.Domain.Entities.CategoryAggregate.Errors;
using Catalog.Domain.Entities.ProductAggregate;
using Catalog.Domain.Entities.PromotionAggregate;
using ErrorOr;
using SharedKernel.Core.Domain;

namespace Catalog.Domain.Entities.CategoryAggregate
{
    /// <summary>
    /// The category.
    /// </summary>
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
        /// Creates a new category.
        /// </summary>
        /// <param name="name">The category name.</param>
        /// <param name="description">The category description.</param>
        /// <returns>The created category, or validation errors.</returns>
        public static ErrorOr<Category> Create(string name, string? description)
        {
            var errors = new List<Error>();

            ValidateNameForCreate(name, errors);
            ValidateDescriptionForCreate(description, errors);

            if (errors.Count != 0)
            {
                return errors;
            }

            var category = new Category
            {
                Name = name,
                Description = description,
            };

            return category;
        }

        /// <summary>
        /// Updates category values.
        /// </summary>
        /// <param name="name">The category name.</param>
        /// <param name="description">The category description.</param>
        /// <returns>An updated result, or validation errors.</returns>
        public ErrorOr<Updated> Update(string? name, string? description)
        {
            var errors = new List<Error>();

            this.UpdateName(name, errors);
            this.UpdateDescription(description, errors);

            if (errors.Count != 0)
            {
                return errors;
            }

            return Result.Updated;
        }

        private static void ValidateNameForCreate(string name, List<Error> errors)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                errors.Add(CategoryErrors.EmptyName);
            }
        }

        private static void ValidateDescriptionForCreate(string? description, List<Error> errors)
        {
            if (string.IsNullOrWhiteSpace(description))
            {
                errors.Add(CategoryErrors.EmptyDescription);
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
                errors.Add(CategoryErrors.EmptyName);
                return;
            }

            if (!string.Equals(this.Name, name, StringComparison.Ordinal))
            {
                this.Name = name;
            }
        }

        private void UpdateDescription(string? description, List<Error> errors)
        {
            if (description is null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(description))
            {
                errors.Add(CategoryErrors.EmptyDescription);
                return;
            }

            if (!string.Equals(this.Description, description, StringComparison.Ordinal))
            {
                this.Description = description;
            }
        }
    }
}

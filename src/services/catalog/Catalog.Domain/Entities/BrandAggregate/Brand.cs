// <copyright file="Brand.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Catalog.Domain.Entities.BrandAggregate.Errors;
using Catalog.Domain.Entities.BrandAggregate.Events;
using Catalog.Domain.Entities.BrandAggregate.ValueObjects;
using Catalog.Domain.Entities.ProductAggregate;
using ErrorOr;
using SharedKernel.Core.Domain;

namespace Catalog.Domain.Entities.BrandAggregate
{
    /// <summary>
    /// The brand.
    /// </summary>
    public class Brand : BaseEntity, IAggregateRoot
    {
        /// <summary>
        /// Gets the name.
        /// </summary>
        public string Name { get; private set; } = default!;

        /// <summary>
        /// Gets the description.
        /// </summary>
        public string? Description { get; private set; } = null!;

        /// <summary>
        /// Gets the website.
        /// </summary>
        public Website? Website { get; private set; }

        /// <summary>
        /// Gets the products.
        /// </summary>
        public ICollection<Product> Products { get; private set; } = [];

        /// <summary>
        /// Creates a brand.
        /// </summary>
        /// <param name="name">The brand name.</param>
        /// <param name="description">The optional brand description.</param>
        /// <param name="website">The optional brand website.</param>
        /// <returns>The created brand, or validation errors.</returns>
        public static ErrorOr<Brand> Create(string name, string? description, string? website)
        {
            var errors = new List<Error>();
            var websiteValue = ValidateWebsiteForCreate(website, errors);

            ValidateNameForCreate(name, errors);
            ValidateDescriptionForCreate(description, errors);

            if (errors.Count != 0)
            {
                return errors;
            }

            var brand = new Brand
            {
                Name = name,
                Description = description,
                Website = websiteValue,
            };

            AddCreatedDomainEvent(brand);
            return brand;
        }

        /// <summary>
        /// Updates a brand.
        /// </summary>
        /// <param name="name">The updated brand name.</param>
        /// <param name="description">The updated brand description.</param>
        /// <param name="website">The updated brand website.</param>
        /// <returns>An updated result, or validation errors.</returns>
        public ErrorOr<Updated> Update(string? name, string? description, string? website)
        {
            var errors = new List<Error>();

            this.UpdateName(name, errors);
            this.UpdateDescription(description, errors);
            this.UpdateWebsite(website, errors);

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
                errors.Add(BrandErrors.EmptyName);
            }
        }

        private static void ValidateDescriptionForCreate(string? description, List<Error> errors)
        {
            if (string.IsNullOrWhiteSpace(description))
            {
                errors.Add(BrandErrors.EmptyDescription);
            }
        }

        private static Website? ValidateWebsiteForCreate(string? website, List<Error> errors)
        {
            if (website is null)
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(website))
            {
                errors.Add(BrandErrors.EmptyWebsite);
                return null;
            }

            return TryCreateWebsite(website, errors);
        }

        private static Website? TryCreateWebsite(string website, List<Error> errors)
        {
            var websiteOrError = Website.Create(website);
            if (!websiteOrError.IsError)
            {
                return websiteOrError.Value;
            }

            errors.AddRange(websiteOrError.Errors);
            return null;
        }

        private static void AddCreatedDomainEvent(Brand brand)
        {
            var brandCreatedDomainEvent = new BrandCreatedDomainEvent(brand.Id, brand.Name);
            brand.AddDomainEvent(brandCreatedDomainEvent);
        }

        private static bool ShouldUpdateWebsite(Website? currentWebsite, Website website)
        {
            return currentWebsite is null || !currentWebsite.Equals(website);
        }

        private void UpdateWebsite(string? website, List<Error> errors)
        {
            if (website is null)
            {
                return;
            }

            var websiteValue = ValidateWebsiteForCreate(website, errors);
            if (websiteValue is null)
            {
                return;
            }

            if (ShouldUpdateWebsite(this.Website, websiteValue))
            {
                this.Website = websiteValue;
            }
        }

        private void UpdateName(string? name, List<Error> errors)
        {
            if (name is null || string.Equals(this.Name, name, StringComparison.Ordinal))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                errors.Add(BrandErrors.EmptyName);
                return;
            }

            this.Name = name;
        }

        private void UpdateDescription(string? description, List<Error> errors)
        {
            if (description is null || string.Equals(this.Description, description, StringComparison.Ordinal))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(description))
            {
                errors.Add(BrandErrors.EmptyDescription);
                return;
            }

            this.Description = description;
        }
    }
}

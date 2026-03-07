// <copyright file="Supplier.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Catalog.Domain.Entities.SupplierAggregate.Errors;
using ErrorOr;
using SharedKernel.Core.Domain;

namespace Catalog.Domain.Entities.SupplierAggregate
{
    /// <summary>
    /// The supplier.
    /// </summary>
    public class Supplier : BaseEntity, IAggregateRoot
    {
        /// <summary>
        /// Gets the name of the supplier.
        /// </summary>
        public string Name { get; private set; } = default!;

        /// <summary>
        /// Gets the description of the supplier.
        /// </summary>
        public string? Description { get; private set; }

        /// <summary>
        /// Gets the website of the supplier.
        /// </summary>
        public string? Website { get; private set; }

        /// <summary>
        /// Creates a supplier.
        /// </summary>
        /// <param name="name">The supplier name.</param>
        /// <param name="description">The optional supplier description.</param>
        /// <param name="website">The supplier website.</param>
        /// <returns>The created supplier, or validation errors.</returns>
        public static ErrorOr<Supplier> Create(
            string name,
            string? description,
            string? website)
        {
            var errors = new List<Error>();

            ValidateNameForCreate(name, errors);
            ValidateDescriptionForCreate(description, errors);
            ValidateWebsiteForCreate(website, errors);

            return errors.Count != 0
                ? errors
                : new Supplier
                {
                    Name = name,
                    Description = description,
                    Website = website,
                };
        }

        /// <summary>
        /// Updates a supplier.
        /// </summary>
        /// <param name="name">The updated supplier name.</param>
        /// <param name="description">The updated supplier description.</param>
        /// <param name="website">The updated supplier website.</param>
        /// <returns>An updated result, or validation errors.</returns>
        public ErrorOr<Updated> Update(
            string? name,
            string? description,
            string? website)
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
                errors.Add(SupplierErrors.EmptyName);
            }
        }

        private static void ValidateDescriptionForCreate(string? description, List<Error> errors)
        {
            if (description is not null && string.IsNullOrWhiteSpace(description))
            {
                errors.Add(SupplierErrors.EmptyDescription);
            }
        }

        private static void ValidateWebsiteForCreate(string? website, List<Error> errors)
        {
            ValidateWebsite(website, errors);
        }

        private static void ValidateWebsite(string? website, List<Error> errors)
        {
            if (string.IsNullOrWhiteSpace(website))
            {
                errors.Add(SupplierErrors.EmptyWebsite);
                return;
            }

            if (!Uri.IsWellFormedUriString(website, UriKind.Absolute)
                || !(website.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || website.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
            {
                errors.Add(SupplierErrors.InvalidWebsite);
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
                errors.Add(SupplierErrors.EmptyName);
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
                errors.Add(SupplierErrors.EmptyDescription);
                return;
            }

            if (!string.Equals(this.Description, description, StringComparison.Ordinal))
            {
                this.Description = description;
            }
        }

        private void UpdateWebsite(string? website, List<Error> errors)
        {
            if (website is null)
            {
                return;
            }

            ValidateWebsite(website, errors);
            if (errors.Count != 0)
            {
                return;
            }

            if (!string.Equals(this.Website, website, StringComparison.Ordinal))
            {
                this.Website = website;
            }
        }
    }
}

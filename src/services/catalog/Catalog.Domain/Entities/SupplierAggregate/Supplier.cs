using Catalog.Domain.Entities.SupplierAggregate.Errors;
using ErrorOr;
using Finbuckle.MultiTenant.Abstractions;
using SharedKernel.Core.Domain;

namespace Catalog.Domain.Entities.SupplierAggregate
{
    /// <summary>
    /// The supplier.
    /// </summary>
    [MultiTenant]
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
        /// Update a brand.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="description"></param>
        /// <param name="website"></param>
        /// <returns></returns>
        public ErrorOr<Updated> Update(
            string? name,
            string? description,
            string? website)
        {
            var errors = new List<Error>();

            if (name is not null)
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    errors.Add(SupplierErrors.EmptyName);
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
                    errors.Add(SupplierErrors.EmptyDescription);
                }
                else if (!string.Equals(Description, description, StringComparison.Ordinal))
                {
                    Description = description;
                }
            }

            if (website is not null)
            {
                if (string.IsNullOrWhiteSpace(website))
                {
                    errors.Add(SupplierErrors.EmptyWebsite);
                }
                else if (!Uri.IsWellFormedUriString(website, UriKind.Absolute)
                    || !(website.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || website.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
                {
                    errors.Add(SupplierErrors.InvalidWebsite);
                }
                else if (!string.Equals(Website, website, StringComparison.Ordinal))
                {
                    Website = website;
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
        /// <param name="website"></param>
        /// <returns></returns>
        public static ErrorOr<Supplier> Create(
            string name, string? description, string? website)
        {
            var errors = new List<Error>();

            if (string.IsNullOrWhiteSpace(name))
            {
                errors.Add(SupplierErrors.EmptyName);
            }

            if (description is not null && string.IsNullOrWhiteSpace(description))
            {
                errors.Add(SupplierErrors.EmptyDescription);
            }

            if (string.IsNullOrWhiteSpace(website))
            {
                errors.Add(SupplierErrors.EmptyWebsite);
            }
            else if (!Uri.IsWellFormedUriString(website, UriKind.Absolute)
                || !(website.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || website.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
            {
                errors.Add(SupplierErrors.InvalidWebsite);
            }

            if (errors.Count != 0)
            {
                return errors;
            }

            Supplier supplier = new()
            {
                Name = name,
                Description = description,
                Website = website
            };

            return supplier;
        }
    }
}

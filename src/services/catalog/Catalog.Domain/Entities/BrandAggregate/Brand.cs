using Catalog.Domain.Entities.BrandAggregate.Errors;
using Catalog.Domain.Entities.BrandAggregate.ValueObjects;
using Catalog.Domain.Entities.ProductAggregate;
using ErrorOr;
using Finbuckle.MultiTenant;
using SharedKernel.Core.Domain;

namespace Catalog.Domain.Entities.BrandAggregate
{
    /// <summary>
    /// The brand.
    /// </summary>
    [MultiTenant]
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
        /// Update a brand.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="description"></param>
        /// <param name="website"></param>
        /// <returns></returns>
        public ErrorOr<Updated> Update(string? name, string? description, string? website)
        {
            var errors = new List<Error>();

            // Name
            if (name is not null && !string.Equals(Name, name, StringComparison.Ordinal))
            {
                if (string.IsNullOrWhiteSpace(name))
                    errors.Add(BrandErrors.EmptyName);
                else
                    Name = name;
            }

            // Description
            if (description is not null && !string.Equals(Description, description, StringComparison.Ordinal))
            {
                if (string.IsNullOrWhiteSpace(description))
                    errors.Add(BrandErrors.EmptyDescription);
                else
                    Description = description;
            }

            // Website (Value Object)
            if (website is not null)
            {
                if (string.IsNullOrWhiteSpace(website))
                {
                    errors.Add(BrandErrors.EmptyWebsite);
                }
                else
                {
                    var websiteOrError = Website.Create(website);
                    if (websiteOrError.IsError)
                    {
                        errors.AddRange(websiteOrError.Errors);
                    }
                    else if (Website is null || !Website.Equals(websiteOrError.Value))
                    {
                        Website = websiteOrError.Value;
                    }
                }
            }

            return errors.Count != 0 ? errors : Result.Updated;
        }

        /// <summary>
        /// Create a brand.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="description"></param>
        /// <param name="website"></param>
        /// <returns></returns>
        public static ErrorOr<Brand> Create(string name, string? description, string? website)
        {
            var errors = new List<Error>();

            if (string.IsNullOrWhiteSpace(name))
            {
                errors.Add(BrandErrors.EmptyName);
            }

            if (string.IsNullOrWhiteSpace(description))
            {
                errors.Add(BrandErrors.EmptyDescription);
            }

            Website? websiteValue = null;
            if (website is not null)
            {
                if (string.IsNullOrWhiteSpace(website))
                {
                    errors.Add(BrandErrors.EmptyWebsite);
                }
                else
                {
                    var websiteOrError = Website.Create(website);
                    if (websiteOrError.IsError)
                    {
                        errors.AddRange(websiteOrError.Errors);
                    }
                    else
                    {
                        websiteValue = websiteOrError.Value;
                    }
                }
            }

            if (errors.Count != 0)
            {
                return errors;
            }

            var brand = new Brand
            {
                Name = name,
                Description = description,
                Website = websiteValue
            };

            return brand;
        }
    }
}

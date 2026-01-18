using Catalog.Application.Brands.Features.DeleteBrand.V1;
using FastEndpoints;
using FluentValidation;

namespace Catalog.Application.Brands.Features.DeleteBrands.V1
{
    /// <summary>
    /// The delete brands validator.
    /// </summary>
    public sealed class DeleteBrandsValidator : Validator<DeleteBrandRequest>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DeleteBrandsValidator"/> class.
        /// </summary>
        public DeleteBrandsValidator()
        {
            RuleFor(brand => brand.Id)
                .NotEmpty();
        }
    }
}

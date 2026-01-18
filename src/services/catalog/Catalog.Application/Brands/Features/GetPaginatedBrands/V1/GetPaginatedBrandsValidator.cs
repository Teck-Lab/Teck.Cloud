using FastEndpoints;
using FluentValidation;

namespace Catalog.Application.Brands.Features.GetPaginatedBrands.V1
{
    /// <summary>
    /// The validator for Pagianted brands.
    /// </summary>
    public sealed class GetPaginatedBrandsValidator : Validator<GetPaginatedBrandsRequest>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GetPaginatedBrandsValidator"/> class.
        /// </summary>
        public GetPaginatedBrandsValidator()
        {
            RuleFor(brand => brand.Page)
                .NotEmpty()
                .GreaterThan(0);
            RuleFor(brand => brand.Size)
                .NotEmpty()
                .GreaterThan(0);
        }
    }
}

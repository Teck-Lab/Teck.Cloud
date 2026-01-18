using FastEndpoints;
using FluentValidation;

namespace Catalog.Application.Brands.Features.DeleteBrand.V1
{
    /// <summary>
    /// The delete brand validator.
    /// </summary>
    public sealed class DeleteBrandValidator : Validator<DeleteBrandRequest>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DeleteBrandValidator"/> class.
        /// </summary>
        public DeleteBrandValidator()
        {
            RuleFor(brand => brand.Id)
                .NotEmpty();
        }
    }
}

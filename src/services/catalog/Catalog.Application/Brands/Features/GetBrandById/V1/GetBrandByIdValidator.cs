using FastEndpoints;
using FluentValidation;

namespace Catalog.Application.Brands.Features.GetBrandById.V1
{
    /// <summary>
    /// The get brand validator.
    /// </summary>
    public sealed class GetBrandByIdValidator : Validator<GetBrandByIdRequest>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GetBrandByIdValidator"/> class.
        /// </summary>
        public GetBrandByIdValidator()
        {
            RuleFor(brand => brand.Id)
                .NotEmpty();
        }
    }
}

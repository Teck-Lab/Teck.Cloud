using FastEndpoints;
using FluentValidation;

namespace Catalog.Application.Brands.Features.UpdateBrand.V1
{
    /// <summary>
    /// The update brand validator.
    /// </summary>
    public sealed class UpdateBrandValidator : Validator<UpdateBrandRequest>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="UpdateBrandValidator"/> class.
        /// </summary>
        public UpdateBrandValidator()
        {
            RuleFor(brand => brand.Id)
                .NotEmpty()
                .WithName("Id");
            RuleFor(brand => brand.Name)
                .NotEmpty()
                .MaximumLength(100)
                .WithName("Name");
        }
    }
}

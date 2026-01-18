using Catalog.Application.Brands.Repositories;
using FastEndpoints;
using FluentValidation;

namespace Catalog.Application.Brands.Features.CreateBrand.V1
{
    /// <summary>
    /// The create brand validator.
    /// </summary>
    public sealed class CreateBrandValidator : Validator<CreateBrandRequest>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CreateBrandValidator"/> class.
        /// </summary>
        public CreateBrandValidator()
        {
            RuleFor(brand => brand.Name)
                .NotEmpty()
                .MaximumLength(100)
                .WithName("Name")
                .MustAsync(async (name, ct) =>
                {
                    // For per-request checks, use Resolve<T>() inside the rule
                    var repo = Resolve<IBrandReadRepository>();
                    return !await repo.ExistsAsync(brand => brand.Name.Equals(name), cancellationToken: ct);
                })
                .WithMessage((_, productSku) => $"Brand with the name '{productSku}' already Exists.");
        }
    }
}

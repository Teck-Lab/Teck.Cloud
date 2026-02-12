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
        private readonly IBrandReadRepository _brandReadRepository;

        /// <summary>
        /// Initializes a new instance of the <see cref="CreateBrandValidator"/> class.
        /// </summary>
        /// <param name="brandReadRepository">The brand read repository.</param>
        public CreateBrandValidator(IBrandReadRepository brandReadRepository)
        {
            _brandReadRepository = brandReadRepository;

            RuleFor(brand => brand.Name)
                .NotEmpty()
                .MaximumLength(100)
                .WithName("Name")
                .MustAsync(async (name, ct) =>
                {
                    return !await _brandReadRepository.ExistsAsync(brand => brand.Name.Equals(name), cancellationToken: ct);
                })
                .WithMessage((_, productSku) => $"Brand with the name '{productSku}' already Exists.");
        }
    }
}

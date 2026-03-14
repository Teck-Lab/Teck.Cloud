// <copyright file="CreateBrandValidator.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Catalog.Application.Brands.Repositories;
using FluentValidation;

namespace Catalog.Application.Brands.Features.CreateBrand.V1
{
    /// <summary>
    /// The create brand validator.
    /// </summary>
    public sealed class CreateBrandValidator : AbstractValidator<CreateBrandRequest>
    {
        private readonly IBrandReadRepository brandReadRepository;

        /// <summary>
        /// Initializes a new instance of the <see cref="CreateBrandValidator"/> class.
        /// </summary>
        /// <param name="brandReadRepository">The brand read repository.</param>
        public CreateBrandValidator(IBrandReadRepository brandReadRepository)
        {
            this.brandReadRepository = brandReadRepository;

            this.RuleFor(brand => brand.Name)
                .NotEmpty()
                .MaximumLength(100)
                .WithName("Name")
                .MustAsync(async (name, ct) =>
                {
                    return !await this.brandReadRepository.ExistsAsync(brand => brand.Name.Equals(name), cancellationToken: ct).ConfigureAwait(false);
                })
                .WithMessage((_, productSku) => $"Brand with the name '{productSku}' already Exists.");
        }
    }
}

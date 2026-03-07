// <copyright file="UpdateBrandValidator.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using FluentValidation;

namespace Catalog.Application.Brands.Features.UpdateBrand.V1
{
    /// <summary>
    /// The update brand validator.
    /// </summary>
    public sealed class UpdateBrandValidator : AbstractValidator<UpdateBrandRequest>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="UpdateBrandValidator"/> class.
        /// </summary>
        public UpdateBrandValidator()
        {
            this.RuleFor(brand => brand.Id)
                .NotEmpty()
                .WithName("Id");
            this.RuleFor(brand => brand.Name)
                .NotEmpty()
                .MaximumLength(100)
                .WithName("Name");
        }
    }
}

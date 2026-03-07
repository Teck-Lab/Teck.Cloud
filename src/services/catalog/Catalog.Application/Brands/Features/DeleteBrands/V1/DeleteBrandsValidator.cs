// <copyright file="DeleteBrandsValidator.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Catalog.Application.Brands.Features.DeleteBrand.V1;
using FluentValidation;

namespace Catalog.Application.Brands.Features.DeleteBrands.V1
{
    /// <summary>
    /// The delete brands validator.
    /// </summary>
    public sealed class DeleteBrandsValidator : AbstractValidator<DeleteBrandRequest>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DeleteBrandsValidator"/> class.
        /// </summary>
        public DeleteBrandsValidator()
        {
            this.RuleFor(brand => brand.Id)
                .NotEmpty();
        }
    }
}

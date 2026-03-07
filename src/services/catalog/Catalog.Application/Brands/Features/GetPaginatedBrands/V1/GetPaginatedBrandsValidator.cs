// <copyright file="GetPaginatedBrandsValidator.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using FluentValidation;

namespace Catalog.Application.Brands.Features.GetPaginatedBrands.V1
{
    /// <summary>
    /// The validator for Pagianted brands.
    /// </summary>
    public sealed class GetPaginatedBrandsValidator : AbstractValidator<GetPaginatedBrandsRequest>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GetPaginatedBrandsValidator"/> class.
        /// </summary>
        public GetPaginatedBrandsValidator()
        {
            this.RuleFor(brand => brand.Page)
                .NotEmpty()
                .GreaterThan(0);
            this.RuleFor(brand => brand.Size)
                .NotEmpty()
                .GreaterThan(0);
        }
    }
}

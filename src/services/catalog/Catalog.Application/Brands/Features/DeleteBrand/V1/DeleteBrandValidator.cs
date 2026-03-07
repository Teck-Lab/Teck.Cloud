// <copyright file="DeleteBrandValidator.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using FluentValidation;

namespace Catalog.Application.Brands.Features.DeleteBrand.V1
{
    /// <summary>
    /// The delete brand validator.
    /// </summary>
    public sealed class DeleteBrandValidator : AbstractValidator<DeleteBrandRequest>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DeleteBrandValidator"/> class.
        /// </summary>
        public DeleteBrandValidator()
        {
            this.RuleFor(brand => brand.Id)
                .NotEmpty();
        }
    }
}

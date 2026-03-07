// <copyright file="GetBrandByIdValidator.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using FluentValidation;

namespace Catalog.Application.Brands.Features.GetBrandById.V1
{
    /// <summary>
    /// The get brand validator.
    /// </summary>
    public sealed class GetBrandByIdValidator : AbstractValidator<GetBrandByIdRequest>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GetBrandByIdValidator"/> class.
        /// </summary>
        public GetBrandByIdValidator()
        {
            this.RuleFor(brand => brand.Id)
                .NotEmpty();
        }
    }
}

// <copyright file="DeleteBrandsValidator.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using FluentValidation;

namespace Catalog.Application.Brands.Features.DeleteBrands.V1
{
    /// <summary>
    /// The delete brands validator.
    /// </summary>
    public sealed class DeleteBrandsValidator : AbstractValidator<DeleteBrandsRequest>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DeleteBrandsValidator"/> class.
        /// </summary>
        public DeleteBrandsValidator()
        {
            this.RuleFor(request => request.Ids)
                .NotEmpty();

            this.RuleForEach(request => request.Ids)
                .NotEmpty();
        }
    }
}

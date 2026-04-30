// <copyright file="GetProductByIdValidator.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using FluentValidation;

namespace Catalog.Application.Products.Features.GetProductById.V1
{
    /// <summary>
    /// The get product by id validator.
    /// </summary>
    public sealed class GetProductByIdValidator : AbstractValidator<GetProductByIdRequest>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GetProductByIdValidator"/> class.
        /// </summary>
        public GetProductByIdValidator()
        {
            this.RuleFor(product => product.ProductId)
                .NotEmpty();
        }
    }
}

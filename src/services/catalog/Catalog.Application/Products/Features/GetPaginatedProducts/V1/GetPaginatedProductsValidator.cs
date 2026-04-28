// <copyright file="GetPaginatedProductsValidator.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using FluentValidation;

namespace Catalog.Application.Products.Features.GetPaginatedProducts.V1
{
    /// <summary>
    /// Validator for paginated product request.
    /// </summary>
    public sealed class GetPaginatedProductsValidator : AbstractValidator<GetPaginatedProductsRequest>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GetPaginatedProductsValidator"/> class.
        /// </summary>
        public GetPaginatedProductsValidator()
        {
            this.RuleFor(request => request.Page)
                .NotEmpty()
                .GreaterThan(0);

            this.RuleFor(request => request.Size)
                .NotEmpty()
                .GreaterThan(0);
        }
    }
}

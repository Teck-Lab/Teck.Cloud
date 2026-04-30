// <copyright file="GetPaginatedSuppliersValidator.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using FluentValidation;

namespace Catalog.Application.Suppliers.Features.GetPaginatedSuppliers.V1
{
    /// <summary>
    /// The validator for paginated suppliers.
    /// </summary>
    public sealed class GetPaginatedSuppliersValidator : AbstractValidator<GetPaginatedSuppliersRequest>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GetPaginatedSuppliersValidator"/> class.
        /// </summary>
        public GetPaginatedSuppliersValidator()
        {
            this.RuleFor(supplier => supplier.Page)
                .NotEmpty()
                .GreaterThan(0);

            this.RuleFor(supplier => supplier.Size)
                .NotEmpty()
                .GreaterThan(0);
        }
    }
}

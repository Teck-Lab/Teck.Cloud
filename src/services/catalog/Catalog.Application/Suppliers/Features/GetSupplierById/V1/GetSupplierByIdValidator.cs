// <copyright file="GetSupplierByIdValidator.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using FluentValidation;

namespace Catalog.Application.Suppliers.Features.GetSupplierById.V1
{
    /// <summary>
    /// The get supplier by id validator.
    /// </summary>
    public sealed class GetSupplierByIdValidator : AbstractValidator<GetSupplierByIdRequest>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GetSupplierByIdValidator"/> class.
        /// </summary>
        public GetSupplierByIdValidator()
        {
            this.RuleFor(supplier => supplier.Id)
                .NotEmpty();
        }
    }
}

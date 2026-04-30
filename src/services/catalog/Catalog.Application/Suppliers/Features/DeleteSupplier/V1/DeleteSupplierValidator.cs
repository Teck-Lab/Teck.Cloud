// <copyright file="DeleteSupplierValidator.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using FluentValidation;

namespace Catalog.Application.Suppliers.Features.DeleteSupplier.V1
{
    /// <summary>
    /// The delete supplier validator.
    /// </summary>
    public sealed class DeleteSupplierValidator : AbstractValidator<DeleteSupplierRequest>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DeleteSupplierValidator"/> class.
        /// </summary>
        public DeleteSupplierValidator()
        {
            this.RuleFor(supplier => supplier.Id)
                .NotEmpty();
        }
    }
}

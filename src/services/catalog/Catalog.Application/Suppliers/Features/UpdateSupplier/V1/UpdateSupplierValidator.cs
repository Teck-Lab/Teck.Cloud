// <copyright file="UpdateSupplierValidator.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using FluentValidation;

namespace Catalog.Application.Suppliers.Features.UpdateSupplier.V1
{
    /// <summary>
    /// The update supplier validator.
    /// </summary>
    public sealed class UpdateSupplierValidator : AbstractValidator<UpdateSupplierRequest>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="UpdateSupplierValidator"/> class.
        /// </summary>
        public UpdateSupplierValidator()
        {
            this.RuleFor(supplier => supplier.Id)
                .NotEmpty()
                .WithName("Id");

            this.RuleFor(supplier => supplier.Name)
                .NotEmpty()
                .MaximumLength(100)
                .WithName("Name");
        }
    }
}

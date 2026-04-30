// <copyright file="CreateSupplierValidator.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Catalog.Application.Suppliers.Repositories;
using FluentValidation;

namespace Catalog.Application.Suppliers.Features.CreateSupplier.V1
{
    /// <summary>
    /// The create supplier validator.
    /// </summary>
    public sealed class CreateSupplierValidator : AbstractValidator<CreateSupplierRequest>
    {
        private readonly ISupplierReadRepository supplierReadRepository;

        /// <summary>
        /// Initializes a new instance of the <see cref="CreateSupplierValidator"/> class.
        /// </summary>
        /// <param name="supplierReadRepository">The supplier read repository.</param>
        public CreateSupplierValidator(ISupplierReadRepository supplierReadRepository)
        {
            this.supplierReadRepository = supplierReadRepository;

            this.RuleFor(supplier => supplier.Name)
                .NotEmpty()
                .MaximumLength(100)
                .WithName("Name")
                .MustAsync(async (name, ct) =>
                {
                    return !await this.supplierReadRepository.ExistsAsync(supplier => supplier.Name.Equals(name), cancellationToken: ct).ConfigureAwait(false);
                })
                .WithMessage((_, supplierName) => $"Supplier with the name '{supplierName}' already Exists.");
        }
    }
}

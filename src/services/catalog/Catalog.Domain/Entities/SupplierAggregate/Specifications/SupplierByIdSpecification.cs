// <copyright file="SupplierByIdSpecification.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Ardalis.Specification;

namespace Catalog.Domain.Entities.SupplierAggregate.Specifications
{
    /// <summary>
    /// Specification to retrieve a supplier by its ID.
    /// </summary>
    public sealed class SupplierByIdSpecification : Specification<Supplier>, ISingleResultSpecification<Supplier>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SupplierByIdSpecification"/> class.
        /// </summary>
        /// <param name="id">The supplier ID.</param>
        public SupplierByIdSpecification(Guid id)
        {
            this.Query.Where(supplier => supplier.Id == id);
        }
    }
}

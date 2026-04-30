// <copyright file="SupplierMappings.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Catalog.Application.Suppliers.Features.CreateSupplier.V1;
using Catalog.Application.Suppliers.Features.GetPaginatedSuppliers.V1;
using Catalog.Application.Suppliers.Features.GetSupplierById.V1;
using Catalog.Application.Suppliers.Features.UpdateSupplier.V1;
using Catalog.Application.Suppliers.ReadModels;
using Catalog.Domain.Entities.SupplierAggregate;
using Riok.Mapperly.Abstractions;

namespace Catalog.Application.Suppliers.Mappings
{
    /// <summary>
    /// The supplier mappings.
    /// </summary>
    [Mapper(RequiredMappingStrategy = RequiredMappingStrategy.None)]
    public static partial class SupplierMapper
    {
        /// <summary>
        /// Maps a domain Supplier to a create supplier response.
        /// </summary>
        /// <param name="supplier">The supplier.</param>
        /// <returns>A create supplier response.</returns>
        public static partial CreateSupplierResponse SupplierToCreateSupplierResponse(Supplier supplier);

        /// <summary>
        /// Maps a domain Supplier to an update supplier response.
        /// </summary>
        /// <param name="supplier">The supplier.</param>
        /// <returns>An update supplier response.</returns>
        public static partial UpdateSupplierResponse SupplierToUpdateSupplierResponse(Supplier supplier);

        /// <summary>
        /// Maps a SupplierReadModel to a get-by-id supplier response.
        /// </summary>
        /// <param name="supplier">The supplier read model.</param>
        /// <returns>A get-by-id supplier response.</returns>
        internal static partial GetByIdSupplierResponse SupplierReadModelToGetByIdSupplierResponse(SupplierReadModel supplier);

        /// <summary>
        /// Maps a SupplierReadModel to a get-paginated-suppliers response.
        /// </summary>
        /// <param name="supplier">The supplier read model.</param>
        /// <returns>A get-paginated-suppliers response.</returns>
        internal static partial GetPaginatedSuppliersResponse SupplierReadModelToGetPaginatedSuppliersResponse(SupplierReadModel supplier);
    }
}

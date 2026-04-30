// <copyright file="GetSupplierById.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Catalog.Application.Suppliers.Mappings;
using Catalog.Application.Suppliers.ReadModels;
using Catalog.Application.Suppliers.Repositories;
using Catalog.Domain.Entities.SupplierAggregate.Errors;
using ErrorOr;
using SharedKernel.Core.CQRS;

namespace Catalog.Application.Suppliers.Features.GetSupplierById.V1
{
    /// <summary>
    /// Get supplier by id query.
    /// </summary>
    public sealed record GetSupplierByIdQuery(Guid Id) : IQuery<ErrorOr<GetByIdSupplierResponse>>;

    /// <summary>
    /// Get supplier by id query handler.
    /// </summary>
    internal sealed class GetSupplierByIdQueryHandler(ISupplierReadRepository supplierReadRepository) : IQueryHandler<GetSupplierByIdQuery, ErrorOr<GetByIdSupplierResponse>>
    {
        private readonly ISupplierReadRepository supplierReadRepository = supplierReadRepository;

        /// <inheritdoc/>
        public async ValueTask<ErrorOr<GetByIdSupplierResponse>> Handle(GetSupplierByIdQuery request, CancellationToken cancellationToken)
        {
            SupplierReadModel? supplier = await this.supplierReadRepository.GetByIdAsync(request.Id, cancellationToken).ConfigureAwait(false);

            return supplier == null
                ? (ErrorOr<GetByIdSupplierResponse>)SupplierErrors.NotFound
                : (ErrorOr<GetByIdSupplierResponse>)SupplierMapper.SupplierReadModelToGetByIdSupplierResponse(supplier);
        }
    }
}

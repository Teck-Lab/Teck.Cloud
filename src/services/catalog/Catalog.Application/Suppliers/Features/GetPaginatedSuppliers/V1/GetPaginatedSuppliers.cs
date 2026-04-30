// <copyright file="GetPaginatedSuppliers.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Catalog.Application.Suppliers.Mappings;
using Catalog.Application.Suppliers.Repositories;
using ErrorOr;
using SharedKernel.Core.CQRS;
using SharedKernel.Core.Pagination;

namespace Catalog.Application.Suppliers.Features.GetPaginatedSuppliers.V1
{
    /// <summary>
    /// Get paginated suppliers query.
    /// </summary>
    public sealed record GetPaginatedSuppliersQuery(int Page, int Size, string? Keyword) : IQuery<ErrorOr<PagedList<GetPaginatedSuppliersResponse>>>;

    /// <summary>
    /// Get paginated suppliers query handler.
    /// </summary>
    internal sealed class GetPaginatedSuppliersQueryHandler(ISupplierReadRepository supplierReadRepository)
        : IQueryHandler<GetPaginatedSuppliersQuery, ErrorOr<PagedList<GetPaginatedSuppliersResponse>>>
    {
        private readonly ISupplierReadRepository supplierReadRepository = supplierReadRepository;

        /// <inheritdoc/>
        public async ValueTask<ErrorOr<PagedList<GetPaginatedSuppliersResponse>>> Handle(GetPaginatedSuppliersQuery request, CancellationToken cancellationToken)
        {
            PagedList<ReadModels.SupplierReadModel> suppliers =
                await this.supplierReadRepository.GetPagedSuppliersAsync(request.Page, request.Size, request.Keyword, cancellationToken).ConfigureAwait(false);

            var responses = suppliers.Items
                .Select(SupplierMapper.SupplierReadModelToGetPaginatedSuppliersResponse)
                .ToList();

            return new PagedList<GetPaginatedSuppliersResponse>(responses, suppliers.TotalItems, request.Page, request.Size);
        }
    }
}

// <copyright file="GetPaginatedTenants.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Customer.Application.Tenants.ReadModels;
using Customer.Application.Tenants.Repositories;
using ErrorOr;
using SharedKernel.Core.CQRS;
using SharedKernel.Core.Pagination;

namespace Customer.Application.Tenants.Features.GetPaginatedTenants.V1;

/// <summary>
/// Query to get paginated tenants.
/// </summary>
/// <param name="Page">Page number (1-based).</param>
/// <param name="Size">Page size.</param>
/// <param name="Keyword">Optional keyword filter for tenant identifier or name.</param>
/// <param name="Plan">Optional tenant plan filter.</param>
/// <param name="IsActive">Optional active/inactive filter.</param>
public sealed record GetPaginatedTenantsQuery(
    int Page,
    int Size,
    string? Keyword,
    string? Plan,
    bool? IsActive)
    : IQuery<ErrorOr<PagedList<GetPaginatedTenantsResponse>>>;

/// <summary>
/// Handler for paginated tenant queries.
/// </summary>
public sealed class GetPaginatedTenantsQueryHandler(ITenantReadRepository tenantReadRepository)
    : IQueryHandler<GetPaginatedTenantsQuery, ErrorOr<PagedList<GetPaginatedTenantsResponse>>>
{
    private readonly ITenantReadRepository tenantReadRepository = tenantReadRepository;

    /// <inheritdoc/>
    public async ValueTask<ErrorOr<PagedList<GetPaginatedTenantsResponse>>> Handle(
        GetPaginatedTenantsQuery query,
        CancellationToken cancellationToken)
    {
        PagedList<TenantReadModel> tenants = await this.tenantReadRepository
            .GetPagedTenantsAsync(query.Page, query.Size, query.Keyword, query.Plan, query.IsActive, cancellationToken)
            .ConfigureAwait(false);

        IList<GetPaginatedTenantsResponse> items = tenants.Items
            .Select(tenant => new GetPaginatedTenantsResponse
            {
                Id = tenant.Id,
                Identifier = tenant.Identifier,
                Name = tenant.Name,
                Plan = tenant.Plan,
                DatabaseStrategy = tenant.DatabaseStrategy,
                IsActive = tenant.IsActive,
                CreatedAt = tenant.CreatedAt,
                UpdatedOn = tenant.UpdatedOn,
            })
            .ToList();

        return new PagedList<GetPaginatedTenantsResponse>(items, tenants.TotalItems, tenants.Page, tenants.Size);
    }
}

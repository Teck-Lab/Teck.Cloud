// <copyright file="TenantReadRepository.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Customer.Application.Tenants.ReadModels;
using Customer.Application.Tenants.Repositories;
using Customer.Infrastructure.Persistence.ReadModels;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Core.Pagination;

namespace Customer.Infrastructure.Persistence.Repositories.Read;

/// <summary>
/// Repository for read operations on Tenant models.
/// </summary>
public sealed class TenantReadRepository : ITenantReadRepository
{
    private const int SeparateReadDatabaseMode = 1;
    private readonly CustomerReadDbContext readDbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantReadRepository"/> class.
    /// </summary>
    /// <param name="readDbContext">The read database context.</param>
    public TenantReadRepository(CustomerReadDbContext readDbContext)
    {
        this.readDbContext = readDbContext;
    }

    /// <inheritdoc/>
    public async Task<TenantReadModel?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await this.readDbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(tenant => tenant.KeycloakOrganizationId == id.ToString(), cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<TenantDatabaseInfoReadModel?> GetDatabaseInfoByIdAsync(Guid id, string? serviceName, CancellationToken cancellationToken)
    {
        TenantReadModel? tenant = await this.GetTenantByKeycloakOrganizationIdAsync(id, cancellationToken).ConfigureAwait(false);

        if (tenant is null)
        {
            return null;
        }

        bool hasReadReplicas = await this.ResolveHasReadReplicasAsync(tenant.Id, serviceName, cancellationToken).ConfigureAwait(false);
        return CreateTenantDatabaseInfoReadModel(tenant, hasReadReplicas);
    }

    /// <inheritdoc/>
    public async Task<PagedList<TenantReadModel>> GetPagedTenantsAsync(
        int page,
        int size,
        string? keyword,
        string? plan,
        bool? isActive,
        CancellationToken cancellationToken)
    {
        IQueryable<TenantReadModel> query = this.readDbContext.Tenants.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(tenant =>
                (tenant.Identifier != null && tenant.Identifier.Contains(keyword)) ||
                (tenant.Name != null && tenant.Name.Contains(keyword)));
        }

        if (!string.IsNullOrWhiteSpace(plan))
        {
            query = query.Where(tenant => tenant.Plan == plan);
        }

        if (isActive.HasValue)
        {
            query = query.Where(tenant => tenant.IsActive == isActive.Value);
        }

        int totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        List<TenantReadModel> items = await query
            .OrderBy(tenant => tenant.Identifier)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PagedList<TenantReadModel>(items, totalCount, page, size);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TenantConnectionSeedReadModel>> ListConnectionSeedsAsync(CancellationToken cancellationToken)
    {
        return await this.readDbContext.Tenants
            .AsNoTracking()
            .Where(tenant => tenant.IsActive)
            .OrderBy(tenant => tenant.Identifier)
            .Select(tenant => new TenantConnectionSeedReadModel
            {
                TenantId = tenant.Id,
                Identifier = tenant.Identifier,
                DatabaseStrategy = tenant.DatabaseStrategy,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private static string? NormalizeServiceName(string? serviceName)
    {
        return string.IsNullOrWhiteSpace(serviceName)
            ? null
            : serviceName.Trim();
    }

    private static TenantDatabaseInfoReadModel CreateTenantDatabaseInfoReadModel(TenantReadModel tenant, bool hasReadReplicas)
    {
        return new TenantDatabaseInfoReadModel
        {
            TenantId = tenant.Id,
            Identifier = tenant.Identifier,
            DatabaseStrategy = tenant.DatabaseStrategy,
            DatabaseProvider = tenant.DatabaseProvider,
            HasReadReplicas = hasReadReplicas,
        };
    }

    private static TenantDatabaseMetadataReadModel? SelectMetadata(List<TenantDatabaseMetadataReadModel> metadataRows, string? serviceName)
    {
        if (serviceName is null)
        {
            return metadataRows.Count > 0
                ? metadataRows[0]
                : null;
        }

        return metadataRows.FirstOrDefault(item => string.Equals(item.ServiceName, serviceName, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<TenantReadModel?> GetTenantByKeycloakOrganizationIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await this.readDbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.KeycloakOrganizationId == id.ToString(),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<bool> ResolveHasReadReplicasAsync(Guid tenantId, string? serviceName, CancellationToken cancellationToken)
    {
        var metadataRows = await this.readDbContext.TenantDatabaseMetadata
            .AsNoTracking()
            .Where(metadata => metadata.TenantId == tenantId)
            .OrderBy(metadata => metadata.ServiceName)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        string? normalizedServiceName = NormalizeServiceName(serviceName);
        var metadata = SelectMetadata(metadataRows, normalizedServiceName);
        return metadata?.ReadDatabaseMode == SeparateReadDatabaseMode;
    }
}

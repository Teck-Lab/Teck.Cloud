// <copyright file="PromotionReadRepository.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Catalog.Application.Promotions.ReadModels;
using Catalog.Application.Promotions.Repositories;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Core.Pagination;
using SharedKernel.Persistence.Database.EFCore;

namespace Catalog.Infrastructure.Persistence.Repositories.Read;

/// <summary>
/// Repository for read operations on Promotion entities.
/// </summary>
public sealed class PromotionReadRepository : GenericReadRepository<PromotionReadModel, Guid, ApplicationReadDbContext>, IPromotionReadRepository
{
    private readonly DbSet<PromotionReadModel> promotions;

    /// <summary>
    /// Initializes a new instance of the <see cref="PromotionReadRepository"/> class.
    /// </summary>
    /// <param name="readDbContext">The read database context.</param>
    public PromotionReadRepository(ApplicationReadDbContext readDbContext)
        : base(readDbContext)
    {
        this.promotions = this.DbContext.Promotions;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PromotionReadModel>> GetAllAsync(CancellationToken cancellationToken)
    {
        return await this.GetAllAsync(false, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<PromotionReadModel?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await this.FindByIdAsync(id, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PromotionReadModel>> GetActivePromotionsAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        return await this.promotions
            .AsNoTracking()
            .Where(promotion => promotion.StartDate <= now && promotion.EndDate >= now)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PromotionReadModel>> GetByCategoryIdAsync(Guid categoryId, CancellationToken cancellationToken)
    {
        // Since the promotion read model doesn't have a direct CategoryId property,
        // this would need to be implemented with a join or custom query in a real implementation
        // This is a placeholder implementation
        return await this.promotions
            .AsNoTracking()
            .Where(promotion => promotion.IsActive)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<PagedList<PromotionReadModel>> GetPagedPromotionsAsync(int page, int size, string? keyword, CancellationToken cancellationToken)
    {
        var query = this.promotions.AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(promotion => promotion.Name.Contains(keyword) ||
                                   (promotion.Description != null && promotion.Description.Contains(keyword)));
        }

        var totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await query.OrderBy(promotion => promotion.StartDate)
                             .Skip((page - 1) * size)
                             .Take(size)
                     .ToListAsync(cancellationToken)
                     .ConfigureAwait(false);

        return new PagedList<PromotionReadModel>(items, totalCount, page, size);
    }
}

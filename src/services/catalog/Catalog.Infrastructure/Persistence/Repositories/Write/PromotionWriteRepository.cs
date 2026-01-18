using Catalog.Domain.Entities.PromotionAggregate;
using Catalog.Domain.Entities.PromotionAggregate.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Persistence.Database.EFCore;

namespace Catalog.Infrastructure.Persistence.Repositories.Write;

/// <summary>
/// Repository for write operations on Promotion entities.
/// </summary>
public sealed class PromotionWriteRepository : GenericWriteRepository<Promotion, Guid, ApplicationWriteDbContext>, IPromotionWriteRepository
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PromotionWriteRepository"/> class.
    /// </summary>
    /// <param name="dbContext">The database context.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    public PromotionWriteRepository(
        ApplicationWriteDbContext dbContext,
        IHttpContextAccessor httpContextAccessor)
        : base(dbContext, httpContextAccessor)
    {
    }

    /// <inheritdoc/>
    public async Task<Promotion?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        return await DbContext.Promotions
            .FirstOrDefaultAsync(promotion => promotion.Name == name, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Promotion>> GetActivePromotionsAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        return await DbContext.Promotions
            .Where(promotion => promotion.ValidFrom <= now && promotion.ValidTo >= now)
            .ToListAsync(cancellationToken);
    }

    // Use the base implementation of FirstOrDefaultAsync; when tracked entity is required,
    // call the overload with `enableTracking: true` in the callers below.
}

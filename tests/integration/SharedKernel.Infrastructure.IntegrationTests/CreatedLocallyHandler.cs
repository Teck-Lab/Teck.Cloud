using Microsoft.EntityFrameworkCore;
using SharedKernel.Core.MultiTenancy;
using SharedKernel.Persistence.Caching;
using ZiggyCreatures.Caching.Fusion;

namespace SharedKernel.Infrastructure.IntegrationTests;

/// <summary>Explicit post-commit cache invalidation and domain-to-integration translation.</summary>
public static class CreatedLocallyHandler
{
    /// <summary>Reads the committed row before invalidating and publishing.</summary>
    /// <param name="message">The durable local event.</param>
    /// <param name="context">The translator's separate transaction context.</param>
    /// <param name="cache">The service cache.</param>
    /// <param name="cancellationToken">Delivery cancellation.</param>
    /// <returns>The cascading integration contract.</returns>
    /// <exception cref="InvalidOperationException">The source row was not committed.</exception>
    public static async Task<PublishedExternally> HandleAsync(CreatedLocally message, MessageDbContext context, IFusionCache cache, CancellationToken cancellationToken)
    {
        if (!await context.Set<StoredRecord>().AnyAsync(row => row.Id == message.RecordId, cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("The domain event arrived before its source commit.");
        }

        await cache.RemoveAsync(CacheKey(message.RecordId), token: cancellationToken).ConfigureAwait(false);
        return new PublishedExternally { RecordId = message.RecordId };
    }

    /// <summary>Gets the fixture's explicit global cache key.</summary>
    /// <param name="id">Record identifier.</param>
    /// <returns>The versioned key.</returns>
    public static string CacheKey(Guid id) => new CacheNamespace("fixture", "record", "1", DataScope.Global).Key(id.ToString("N"));
}

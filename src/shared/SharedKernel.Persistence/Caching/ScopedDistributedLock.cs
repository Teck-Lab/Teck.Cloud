using Medallion.Threading;
using Medallion.Threading.Redis;

namespace SharedKernel.Persistence.Caching;

/// <summary>Explicitly namespaced Redis locks; database constraints remain the final correctness boundary.</summary>
/// <param name="connection">The shared host Redis connection.</param>
public sealed class ScopedDistributedLock(RedisConnection connection)
{
    /// <summary>Acquires a lease for an explicitly scoped resource.</summary>
    /// <param name="cacheNamespace">The validated global/tenant namespace.</param>
    /// <param name="resource">The resource identifier.</param>
    /// <param name="timeout">Maximum acquisition wait.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>The lease, which must be disposed after the protected operation.</returns>
    public ValueTask<IDistributedSynchronizationHandle> AcquireAsync(CacheNamespace cacheNamespace, string resource, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cacheNamespace);
        return AcquireCoreAsync(cacheNamespace.Key(resource), timeout, cancellationToken);
    }

    private async ValueTask<IDistributedSynchronizationHandle> AcquireCoreAsync(string key, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var multiplexer = await connection.GetAsync(cancellationToken).ConfigureAwait(false);
        var gate = new RedisDistributedLock("lock/" + key, multiplexer.GetDatabase());
        return await gate.AcquireAsync(timeout, cancellationToken).ConfigureAwait(false);
    }
}

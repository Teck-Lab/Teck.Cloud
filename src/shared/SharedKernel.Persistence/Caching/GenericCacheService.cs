using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using SharedKernel.Core.Caching;
using SharedKernel.Core.Domain;
using ZiggyCreatures.Caching.Fusion;

namespace SharedKernel.Persistence.Caching;

/// <summary>Caches serialized read projections, returning a fresh object on every read.</summary>
/// <typeparam name="TEntity">The immutable/read projection contract, not a tracked write entity.</typeparam>
/// <typeparam name="TId">The identifier.</typeparam>
public sealed class GenericCacheService<TEntity, TId> : IGenericCacheService<TEntity, TId>
    where TEntity : class
{
    private readonly IFusionCache _cache;
    private readonly CacheNamespace _namespace;
    private readonly JsonTypeInfo<TEntity> _json;
    private readonly Func<TId, CancellationToken, Task<TEntity?>> _load;
    private readonly Func<TId, string> _formatId;
    private readonly FusionCacheEntryOptions _options;

    /// <summary>Initializes a new instance of the <see cref="GenericCacheService{TEntity, TId}"/> class.</summary>
    /// <param name="cache">The shared cache.</param>
    /// <param name="cacheNamespace">An explicit ownership namespace matching the loader.</param>
    /// <param name="json">Source-generated JSON metadata for the read projection.</param>
    /// <param name="load">An untracked, scope-enforcing read operation.</param>
    /// <param name="formatId">Invariant identifier formatting.</param>
    /// <param name="duration">The maximum cache lifetime; fail-safe stale reads are disabled.</param>
    /// <exception cref="ArgumentException">A tenant projection uses a global cache namespace.</exception>
    public GenericCacheService(IFusionCache cache, CacheNamespace cacheNamespace, JsonTypeInfo<TEntity> json, Func<TId, CancellationToken, Task<TEntity?>> load, Func<TId, string> formatId, TimeSpan duration)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(duration, TimeSpan.Zero);
        if (typeof(ITenantScoped).IsAssignableFrom(typeof(TEntity)) && cacheNamespace.Scope.TenantId is null)
        {
            throw new ArgumentException("Tenant projections require a tenant cache namespace.", nameof(cacheNamespace));
        }

        _cache = cache;
        _namespace = cacheNamespace;
        _json = json;
        _load = load;
        _formatId = formatId;
        _options = new FusionCacheEntryOptions
        {
            Duration = duration,
            IsFailSafeEnabled = false,
            AllowBackgroundDistributedCacheOperations = false,
            AllowBackgroundBackplaneOperations = false,
        };
    }

    /// <inheritdoc/>
    public async Task<TEntity?> GetOrSetByIdAsync(TId id, CancellationToken cancellationToken = default)
    {
        var bytes = await _cache.GetOrSetAsync<byte[]>(Key(id), (_, token) => LoadBytesAsync(id, token), options: _options, token: cancellationToken).ConfigureAwait(false);
        return Read(bytes);
    }

    /// <inheritdoc/>
    public async Task<TEntity?> TryGetByIdAsync(TId id, CancellationToken cancellationToken = default)
    {
        var value = await _cache.TryGetAsync<byte[]>(Key(id), _options, cancellationToken).ConfigureAwait(false);
        return value.HasValue ? Read(value.Value) : null;
    }

    /// <inheritdoc/>
    public Task SetAsync(TId id, TEntity entity, CancellationToken cancellationToken = default)
    {
        ValidateOwnership(entity);
        return _cache.SetAsync(Key(id), JsonSerializer.SerializeToUtf8Bytes(entity, _json), _options, token: cancellationToken).AsTask();
    }

    /// <inheritdoc/>
    public Task ExpireAsync(TId id, CancellationToken cancellationToken = default) =>
        _cache.ExpireAsync(Key(id), _options, cancellationToken).AsTask();

    /// <inheritdoc/>
    public Task RemoveAsync(TId id, CancellationToken cancellationToken = default) =>
        _cache.RemoveAsync(Key(id), _options, cancellationToken).AsTask();

    private async Task<byte[]> LoadBytesAsync(TId id, CancellationToken cancellationToken)
    {
        var entity = await _load(id, cancellationToken).ConfigureAwait(false);
        ValidateOwnership(entity);
        return JsonSerializer.SerializeToUtf8Bytes(entity!, _json);
    }

    private TEntity? Read(byte[] bytes)
    {
        var entity = JsonSerializer.Deserialize(bytes, _json);
        ValidateOwnership(entity);
        return entity;
    }

    private void ValidateOwnership(TEntity? entity)
    {
        if (entity is ITenantScoped tenant && !string.Equals(tenant.TenantId, _namespace.Scope.TenantId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Cached projection ownership does not match its namespace.");
        }
    }

    private string Key(TId id) => _namespace.Key(_formatId(id));
}

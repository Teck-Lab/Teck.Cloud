# Distributed caching and gateway responses

Service hosts use Redis Sentinel through `AddFoundationRedis`. A standalone endpoint
without `serviceName` is rejected. `AddFoundationStandaloneRedis` exists explicitly
for isolated Testcontainers integration tests; the Aspire E2E topology uses Sentinel.

```csharp
builder.Services.AddFoundationRedis(
    builder.Configuration.GetConnectionString("redis")
    ?? throw new InvalidOperationException("Redis Sentinel configuration is required."));
```

Supply multiple Sentinel seed endpoints and the monitored service name, for example
`sentinel-1:26379,sentinel-2:26379,sentinel-3:26379,serviceName=teck,abortConnect=false`.
Redis credentials use `user`/`password`; separate Sentinel credentials use
`sentinelUser`/`sentinelPassword`. Keep credentials in secret configuration. Production
transport security and ACLs belong to the deployed Redis/Sentinel environment.

StackExchange.Redis discovers the primary and maintains the connection through failover.
The cache, Redis backplane, distributed locker, application lock and health check use the
same shared data multiplexer. Sentinel discovery itself maintains its control connections;
this does not mean the whole topology has only one TCP socket. Do not resolve the primary
once and place its address into static configuration.

## FusionCache layers

| Component           | Configuration                                                                            |
| ------------------- | ---------------------------------------------------------------------------------------- |
| L1                  | Per-process memory, bounded entry lifetime, no stale fail-safe fallback                  |
| L2                  | Redis through the Sentinel-managed shared multiplexer                                    |
| Serialization       | Published Cysharp MemoryPack serializer, versioned `teck:mp1:` key prefix                |
| Backplane           | Published StackExchangeRedis backplane; invalidates other replicas' L1 entries           |
| Cache-fill locking  | Published Redis distributed locker coordinates `GetOrSetAsync` factories across replicas |
| Application locking | Existing `ScopedDistributedLock`, separate from automatic cache-fill locking             |

Each named cache owns its backplane/locker adapter while sharing the connection. Replicas
must use the same logical cache name; different services/gateways must not share an output
namespace. The `mp1` prefix isolates the new binary representation from old JSON cache
entries without deleting shared Redis data. Bump the format namespace when incompatible
payload/serializer changes require it.

`GenericCacheService` still uses generated JSON metadata to produce detached, ownership-
validated projection bytes. MemoryPack encodes FusionCache's distributed envelope and those
bytes. This preserves the existing projection API; it does not convert every projection
into a generated MemoryPack DTO. Direct custom DTO caching needs MemoryPack-compatible
contracts/formatters. Neither this adapter nor the complete application is claimed to be
NativeAOT-verified.

Use Redis as a cache. Sentinel replication is asynchronous: acknowledged cache writes and
lock state can be lost during failover. Backplanes and locks do not create a transaction
with the database. Keep TTLs bounded, invalidate after commit and retain database constraints,
idempotency and fencing where correctness requires them. Never cache authorization or
currentness decisions as the final authority.

## FastEndpoints and gateway output caching

FastEndpoints can use ASP.NET Core's native `CacheOutput` metadata. The published
`ZiggyCreatures.FusionCache.AspNetCore.OutputCaching` adapter supplies `IOutputCacheStore`;
there is no custom endpoint base class or response-cache protocol.

```csharp
// After AddFoundationRedis. Include environment and gateway identity.
builder.Services.AddFoundationOutputCaching("development-customer-gateway");

// Optional feature-specific native named policy.
builder.Services.AddOutputCache(options =>
    options.AddFoundationPublicPolicy("catalog", TimeSpan.FromSeconds(20), "catalog"));
```

Inside a FastEndpoints endpoint's normal `Configure()`:

```csharp
Get("/catalog");
AllowAnonymous();
Options(builder => builder
    .WithMetadata(OperationScope.Global)
    .CacheOutput("catalog"));
```

The built-in `OutputCachingExtensions.PublicPolicy` lasts 30 seconds and uses the `public`
tag. Gateway replicas share output through a separate named FusionCache cache. Customer and
admin gateways use different names, even if their routes or public host names overlap.

`UseFoundationApi` installs output caching when the store is registered, after operation
scope middleware and before FastEndpoints. Host routing, authentication and authorization
must run first. For a proxy-only pipeline use standard `UseOutputCache` in that same position
and attach a selected policy to the mapped route. Upstream invalidation must reach the gateway;
a backend cache eviction does not automatically invalidate gateway HTTP responses.

No routes are cached automatically. The public policies require explicit global operation
metadata and reject authenticated users, Authorization headers and request cookies. Native
ASP.NET rules restrict caching to successful GET/HEAD responses and reject Set-Cookie.
The additional policy rejects private/no-store/no-cache responses. Query strings, host,
Accept and Accept-Language participate in variation; bodies are limited to 1 MiB.
Tenant endpoints, personalized pages, admin data and authorization-dependent responses remain
uncached until a dedicated, tested identity-aware policy is designed. Do not treat the
presence of a tenant header as a validated tenant identity.

Evict a feature after its successful mutation using the standard interface:

```csharp
await outputCacheStore.EvictByTagAsync("catalog", cancellationToken);
```

Register lifetime and tags together in one named policy. Multiple `CacheOutput` calls on the
same endpoint can replace policy metadata; they are not a safe way to extend a named policy.
Response caching/`ResponseCache()` controls a different HTTP caching mechanism and does not
select this distributed output store.

The output store uses Get/Set, not a FusionCache factory. Therefore HTTP miss coalescing
remains ASP.NET Core's per-process locking; the Redis distributed locker protects application
`GetOrSetAsync` factories, not simultaneous HTTP rendering across every gateway replica.

## Verification

Testcontainers tests exercise independent hosts sharing Redis, namespace/tenant isolation,
detached projection copies, backplane removals, one concurrent application cache fill,
HTTP response sharing, query/gateway separation, credential/cookie exclusion and tag eviction.
The [Aspire Sentinel topology](redis-sentinel.md) supplies the real primary-loss E2E test.
There are no real gateway applications yet; the HTTP tests use a FastEndpoints fixture.

## References

- [FusionCache distributed stampede protection](https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/CacheStampede.md)
- [FusionCache output adapter](https://github.com/ZiggyCreatures/FusionCache/tree/main/src/ZiggyCreatures.FusionCache.AspNetCore.OutputCaching)
- [FastEndpoints output caching](https://fast-endpoints.com/docs/response-caching)
- [StackExchange.Redis Sentinel configuration](https://github.com/StackExchange/StackExchange.Redis/blob/main/docs/Configuration.md)

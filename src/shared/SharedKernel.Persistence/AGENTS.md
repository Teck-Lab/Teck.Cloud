# SharedKernel.Persistence: storage and caching adapters

Read [shared runtime](../../../docs/shared-runtime.md) before persistence wiring,
[specifications](../../../docs/specifications.md) for repository execution, and
[multi-tenancy](../../../docs/multi-tenancy.md) for scope/filter changes.

- Read contexts use untracked queries, save guards and read-only credentials.
  Write repositories load tracked aggregate roots. Preserve query filters on both
  sides; specifications cannot override tenant isolation or tracking ownership.
- Unique lookups use single-result specifications and reject duplicate matches;
  Take(1) must not hide duplicates. Unsupported post-processing fails explicitly.
- IUnitOfWork and repositories share the scoped WriteDbContext. Wolverine Eager
  middleware owns handler transaction/outbox completion. Outside handlers use the
  official scoped outbox path; do not add an independently committing adapter.
- Finbuckle enforces shared-database row scope; Wolverine carries envelope identity.
  Do not stack Wolverine conjoined EF stamping onto this context profile.
- Concrete models and migrations remain service-owned. PostgreSQL is tested;
  other providers require their own real query/transaction/migration evidence.
- For caches/locks read [caching](../../../docs/caching.md) and
  [Redis Sentinel](../../../docs/redis-sentinel.md). Runtime Redis uses Sentinel,
  shared async connections, published MemoryPack/backplane/locker adapters and
  versioned tenant-aware namespaces. Standalone Redis is for isolated tests.
- Cache detached projections, bound TTLs and invalidate after commit. Redis locks
  and invalidation are not atomic with the database or final authorization.
- Verify storage/filter/outbox/cache effects with Testcontainers integration tests,
  including cross-tenant rejection and rollback, rather than mocked DbContext.

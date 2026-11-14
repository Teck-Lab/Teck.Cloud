# Shared persistence and infrastructure

The shared runtime is split into three projects:

| Project                     | Owns                                                                                                        |
| --------------------------- | ----------------------------------------------------------------------------------------------------------- |
| SharedKernel.Core           | Domain/events/CQRS and storage interfaces; plain operation/tenant contracts                                 |
| SharedKernel.Persistence    | EF repositories/transactions, optional entity behaviors, Finbuckle enforcement, FusionCache/Redis and locks |
| SharedKernel.Infrastructure | HTTP/auth/gRPC, messaging, host diagnostics, settings and OpenAPI/Scalar                                    |

Services still have Domain, Application and Host projects. Their concrete entities,
mappings, migrations and business rules stay with the service. This does not add a
fourth Persistence project to every service.

## Database composition

Derive the read context from ScopedDbContext with `readOnly: true`, and the
Wolverine-backed write context from WriteDbContext. Its constructor receives
DbContextOptions, OperationContext, IDbContextOutbox and IMessageContext; optional
TimeProvider remains supported. Use separate database credentials with
SELECT-only privileges. No-tracking and rejected SaveChanges are additional guards,
not substitutes for database permissions: raw SQL must also be denied by the database.

Configure service entities **before** calling `base.OnModelCreating(modelBuilder)`.
The base configures only the behaviors an entity opts into:

- ITenantScoped: Finbuckle filter plus TenantId concurrency protection.
- ISoftDeletable: named SoftDelete filter and soft deletion on SaveChanges.
- IAuditable: actor/update auditing.
- IHasDomainEvents: ignore the event collection in the relational model.

ScopedDbContext installs its scope/audit/delete interceptors itself. Do not register
a second copy on the same context. Inject TimeProvider through its optional clock
argument for deterministic audit timestamps. Core's original CreatedAt construction
timestamp remains unchanged.

```csharp
builder.AddFoundationHost();
builder.Services.AddDbContext<ReadContext>(options =>
    options.UseNpgsql(readConnection));
builder.Services.AddDbContextWithWolverineIntegration<WriteContext>(
    options => options.UseNpgsql(writeConnection), "orders_messages");
builder.Services.AddRepositories<Order, Guid, ReadContext, WriteContext>();

builder.UseWolverine(options =>
{
    options.UseFoundationMessaging<WriteContext>(writeConnection, "orders_messages");
    options.UseRequiredRabbitMq(brokerUri);
    options.RouteDomainEvent<OrderPlaced>("orders-local");
    options.RouteIntegrationEvent<OrderPlacedV1>("orders-events");
    // Add explicit listeners and service-specific bounded retry policies.
});
```

Register providers explicitly; no reflection-driven provider selection or automatic
application migration is installed. PostgreSQL is tested. SQL Server and
MySQL/MariaDB remain provider-validation work, including separate migrations.
A provider-neutral model is not proof that every provider works.

Write repositories and AddRepositories require IAggregateRoot. Mark only the
aggregate entry point, not every child entity. AddReadRepository registers a
read-only repository for a child or projection without granting write access.

Repository reads are untracked. Use LoadForUpdateAsync for tracked mutations.
List materialization and mutation batches default to 1,000 rows; use paged
specifications for larger reads. Projection paging is applied once. Specifications
cannot enable tracking or ignore query filters.

The retained ExecuteSoftDelete*/ExecuteHardDelete* names now **stage a bounded,
tracked batch**, then the transaction owner saves it. Hard deletion disables only
the SoftDelete filter and retains tenant filtering. These are not direct bulk SQL
methods. Service domain methods must still raise required business events.

## Global, tenant and mixed data

OperationContext is scoped, starts uninitialized and cannot change identity.
DataScope.Global is explicit; DataScope.ForTenant requires a nonblank identity.
Global models need no tenant directory. Tenant-owned data never uses a fake tenant.

Contexts may be constructed before Wolverine invokes middleware. Scope is required
when data is used; command interception also covers cached query plans and direct
SQL. The operation remains immutable once initialized.

HTTP endpoints attach OperationScope.Global or OperationScope.Tenant metadata.
Run routing, authentication and authorization **before** UseFoundationApi.
Unclassified endpoints leave data scope uninitialized. Tenant ingress requires an
authenticated `tenant_id` claim, a single distinct tenant, and an active
ITenantDirectory result. Raw x-tenant-id headers are not authority. Global metadata
does not grant authorization; endpoints still need their appropriate policies.

Message contracts explicitly implement IGlobalMessage or ITenantMessage. Tenant
middleware requires native envelope TenantId and verifies active ownership and
consistency with the contract. A tenant ID only in the payload is rejected.
At an authorized FastEndpoints/gRPC entry point, invoke with Wolverine
`InvokeForTenantAsync<TResponse>(operation.Scope.TenantId!, command, cancellation)`
or supply `DeliveryOptions.TenantId` when publishing. Do not assume Wolverine.HTTP
tenant detection runs inside FastEndpoints.
Wolverine's internal `*DEFAULT*` envelope value is normalized only as "no envelope
tenant"; it is never accepted as an application tenant. The broker and publishers
must be authenticated and authorized. An arbitrary publisher with broker access
cannot be made trustworthy by a tenant string.

UseFoundationMessaging explicitly service-locates OperationContext so EF and
generated middleware share one scoped identity. Wolverine
WithDbContextAbstraction<IUnitOfWork, TWrite> resolves the actual scoped write
context; repositories and IUnitOfWork share that instance. The global
service-location policy remains unchanged. Concurrent tenant commands, durable
local translation and RabbitMQ consumers are tested with isolated tenant receipts.

Background code creates a DI scope and initializes OperationContext from an
authorized job identity before data access. ITenantDirectory and tenant/database
provisioning remain service-owned. The existing Core connection lookup rejects
missing tenant Read/Write entries; it does not provision databases or choose a
provider.

## Transaction and event ownership

IUnitOfWork is the application's SaveChangesAsync boundary. It no longer exposes
Dispose or manual begin/commit/rollback: normal handlers do not own those lifetimes.
There is no throwing WolverineUnitOfWork adapter.

UseFoundationMessaging<TWrite> registers the actual WriteDbContext as IUnitOfWork
and teaches Wolverine that abstraction through WithDbContextAbstraction. Handlers
can depend only on repositories and IUnitOfWork. The Eager transactional middleware
starts the transaction, the handler may save through IUnitOfWork, and Wolverine
collects domain events and commits the inbox/outbox when the handler succeeds.
Saving is not an early commit: an exception after saving rolls back the write.
Lightweight mode is deliberately not supported by this application save contract.

Outside a message handler, IUnitOfWork enrolls the same write context in the official
scoped IDbContextOutbox, sets its validated tenant identity, and calls
SaveChangesAndFlushMessagesAsync. This collects pending entity events and persists
their durable envelopes with the entity changes. Repeated application saves use
Wolverine's explicit MultiFlushMode.AllowMultiples. The returned count is EF state
entries, potentially including tracked messaging entries, not a business row count.
After a failed save, discard the operation scope; do not retry its drained domain
events in place. Retry the complete command in a new scope.

When application code explicitly publishes messages outside handlers, use the
scoped IDbContextOutbox<TWrite> and its SaveChangesAndFlushMessagesAsync directly.
Do not publish through an unrelated IMessageBus and expect a later unit-of-work
save to capture it. Caller-owned explicit transactions must use that official
outbox path deliberately; ordinary IUnitOfWork saves reject them.

AddEfUnitOfWork<TContext> remains an opt-in EF-only helper for contexts without
Wolverine publication. Its concrete helper can manage transactions it owns, never
disposes the injected context, and rejects pending domain events. Do not register
it alongside the Wolverine-backed IUnitOfWork in one host.

The foundation enables Wolverine durable local queues, durable inboxes on
listeners and durable outboxes on sending endpoints by default.
Domain events use durable local queues. Translators publish **separate**
IIntegrationEvent contracts to explicit RabbitMQ exchanges. The routing helper
rejects a contract that is also IDomainEvent; do not add catch-all external
publication conventions alongside it.

Durable domain-event handlers can invalidate caches after the original commit.
They retry independently; this is eventual consistency. A database commit and Redis
invalidation are not one atomic transaction. Bound TTLs, design idempotent handlers,
and do not use caches as the final authorization/currentness boundary.
The integration fixture proves committed-row visibility before invalidation,
preserved cache after save-then-throw rollback, broker delivery and retry, duplicate
physical-envelope suppression by the durable inbox, and pending outbox delivery
after a broker outage and graceful application restart. These fixtures do not
claim coverage of every abrupt process-kill timing or business-level idempotency.

ErrorOr<T> remains the typed handler result. An ErrorOr failure does not magically
roll back arbitrary prior mutations. Validate before mutation; explicitly test
rejected commands and throwing failures. Broker consumers must throw classified
transient failures when retry is needed, rather than return an unnoticed error.
Use database constraints/business idempotency keys; no Redis "seen before execution"
deduplication layer is installed.

## Caching and locks

AddFoundationCache is memory-only. AddFoundationRedis requires Sentinel configuration and installs MemoryPack-backed Redis storage, a backplane, distributed cache-fill locking and ScopedDistributedLock. AddFoundationStandaloneRedis is reserved for isolated standalone integration tests. See [distributed caching](caching.md) and [local Sentinel](redis-sentinel.md). Both use bounded defaults without
stale fail-safe reads. Redis components share one asynchronous multiplexer for the
host lifetime; initialization is retryable and caller cancellation is observed.

GenericCacheService caches serialized read projections and returns detached copies.
Supply generated JsonTypeInfo, an invariant ID formatter, a scoped untracked loader,
an explicit duration and CacheNamespace(service, contract, version, scope).
Namespace components are escaped. Tenant-marked projections reject global
namespaces and mismatched loaded/stored ownership. Other DTOs need a namespace
matching the loader's validated data scope.

RemoveAsync/ExpireAsync do not infer a database commit. Call them from an
appropriate post-commit durable handler. Redis/backplane consistency and network
failures do not provide exactly-once business effects. Keep distributed-lock leases
short and retain database constraints/fencing where correctness requires it.

## HTTP, gRPC and hosting

AddFoundationApi accepts FastEndpoints.Generator's DiscoveredTypes.All lists.
Endpoints inherit standard FastEndpoints types; there is no shared custom endpoint
hierarchy. Use `Send.ResultAsync(result.ToHttpResult(MyJsonContext.Default.Response))`.
Use generated Web JSON options when camelCase is required. Validation errors map to
standard validation problems; unexpected error descriptions are not exposed.

UseFoundationApi can explicitly expose OpenAPI and Scalar. Documentation access is
a host/ingress policy decision. AddFoundationAuthentication requires an HTTPS
issuer and audience; it does not disable validation for development.
Configure CORS and trusted proxies using standard ASP.NET Core APIs for each host;
the foundation neither enables wildcard CORS nor clears trusted-proxy restrictions.

AddOperationCredentials adds asynchronous gRPC call credentials from the scoped
OperationContext and IAccessTokenProvider. It observes cancellation and works
without HttpContext. A receiving service must validate tenant authority against
authenticated claims; the propagated header alone grants nothing. Client-factory
behavior is tested using a controlled HTTP/2 response; live mTLS/identity-provider
interoperability remains an application E2E concern.

OAuthTokenClient supports explicit service credentials and RFC 8693 exchange.
AddOAuthTokenClient disables redirects/cookies and bounds timeouts, with no automatic
POST retries. Services implement IAccessTokenProvider to choose the correct grant
and token cache keyed by actor/tenant/audience. No universal token cache or inferred
user delegation is installed. Credentials come from host secret configuration.

AddFoundationHost installs scoped foundations, Serilog, problem details,
OpenTelemetry instrumentation and health registration without opening a database
or broker connection. OTLP export activates when a collector endpoint is configured;
see [observability](observability.md) for integrations, Serilog, sampling and export.
MapFoundationHealth maps live/startup/ready/dependencies. Required dependency checks are installed by default; PostgreSQL checks follow provider composition. See [service health](health.md) for registration, lifecycle semantics and safe diagnostics. AddValidatedSettings accepts
service-owned source-generated options validators.

## Verification and limits

Run fast tests with `bun run test`; real PostgreSQL, Redis and RabbitMQ tests with
`bun run test:integration`. Testcontainers needs a working Docker-compatible API
(Docker Engine or Podman's API); Docker Sandboxes are a separate runtime.

`bun run verify:wolverine-static` generates the fixture's exercised handlers,
rebuilds/reruns in mandatory Static mode with WolverineFx.RuntimeCompilation
excluded, and restores normal project assets afterwards. The compiler package is
test-only. This proves the exercised generated-handler path, not full NativeAOT,
all service handlers or EF compiled models.

The API fixture uses WebApplicationFactory, generated endpoint discovery and
standard FastEndpoints to check responses, error mapping, health, OpenAPI and
Scalar. It is test code, not a sample production service to deploy.

OpenBao connection provisioning, flags, business audit publication, actual service
permissions and per-provider migrations are deliberately not ported without a
consumer. Aspire application/Kubernetes E2E and live identity/gRPC TLS tests belong
with the first real services. No cloud infrastructure or existing Monorepo code
was changed.

UseRequiredRabbitMq also enables Wolverine RabbitMQ control queues for node
coordination. Grant its broker identity permission to create and use the
node-specific system queues; PostgreSQL still stores durable inbox/outbox data.

Specification queries now support unique entity/DTO reads and tracked aggregate
loading with includes. See [query specifications](specifications.md) for operation
semantics, tenancy/tracking safeguards, SQL projections and supported Ardalis features.

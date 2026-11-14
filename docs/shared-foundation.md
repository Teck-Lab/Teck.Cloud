# SharedKernel foundation

SharedKernel.Core has been carried into src/shared/SharedKernel.Core from
Teck.Monorepo. Pricing, Licensing and Devices were excluded. The namespace and
main patterns are retained so subsequent service migrations can reuse the existing
groundwork. This replaces the earlier proposal to split Core into several small
projects before migrating it.

## Included

| Area               | Available now                                                                                                        |
| ------------------ | -------------------------------------------------------------------------------------------------------------------- |
| Domain             | BaseEntity / BaseEntity<TId>, aggregate/read-model markers, value objects, auditing, soft deletion and ITenantScoped |
| Events             | Domain/integration event interfaces and bases; event identity, occurrence time and string metadata                   |
| CQRS               | Command/query/transactional markers, handler contracts and Unit                                                      |
| Database           | Read/write repository contracts, specification queries, IUnitOfWork and credential models                            |
| Caching            | IGenericCacheService contract                                                                                        |
| Tenant connections | Separate read/write connection lookup for an explicitly supplied tenant                                              |
| Pagination         | Paging input and PagedList<T>                                                                                        |
| Mapping            | Existing Mapperly configuration                                                                                      |
| Options            | IOptionsRoot marker                                                                                                  |
| Exceptions         | Existing exception types with correctness fixes                                                                      |

Options contains only IOptionsRoot. Keep it as a convention for typed settings.
The marker does not bind or validate anything. Host composition should use typed
options, startup validation and supported generated binding; the old reflection-based
LoadOptions/BindValidateReturn helpers have not been imported.

The excluded Pricing folder also contained DatabaseStrategy, DatabaseProvider and
DatabaseOptions with pricing/cost/SLA policy embedded in them. Those types were
excluded with Pricing. When runtime tenant storage needs topology/provider types,
introduce neutral configuration in the appropriate database adapter rather than
restoring those business policies.

## Changes made during import

- Fixed ValueObject equality for unequal-length component sequences.
- Reject null domain events; retained ordered snapshot/drain behavior.
- Hide DomainEvents from JSON and leave UpdatedOn unset until an actual update.
- Require positive page/size and nonnegative result totals; retain the page-size cap.
- Escape PostgreSQL-style credential values with DbConnectionStringBuilder rather
  than concatenating them. Reject connection-identity overrides through additional
  parameters. Credential record ToString output is redacted; never log or serialize
  credential objects as diagnostic data.
- Preserve the inner exception in ConfigurationMissingException.
- Remove tracking switches from read and cache contracts. Add LoadForUpdateAsync
  on the write contract for explicitly tracked mutations. The existing bulk-write
  contracts remain but require tenant/event/audit handling in their implementation.
- Preserve event ID/time during source-generated JSON replay with init properties.
  Metadata values are strings, not arbitrary objects; business fields remain typed.
- Reject empty tenant identifiers and configuration path delimiters in connection
  lookup. This is configuration lookup, not tenant authentication/authorization.
- Fix namespaces, documentation and analyzer findings without importing the old
  blanket warning suppressions.

The retained BaseEntity still includes auditing and soft deletion for compatibility
with the requested groundwork. ScopedDbContext applies these behaviors through
its interceptors; consumers may implement narrower entity interfaces when those
behaviors are inappropriate. Core alone does not register any EF behavior.
The retained exception status-code properties are compatibility metadata; new
expected application failures use ErrorOr and transport mapping.

## Dependencies and ownership

Core uses Ardalis.Specification, Microsoft.Extensions.Configuration.Abstractions,
NewId and Mapperly. It no longer has the broad Microsoft.AspNetCore.App framework
reference or unused Finbuckle, SmartEnum, Nanoid and ErrorOr package references.
This is a reusable foundation with narrow library dependencies, not a claim that
the whole Core assembly has zero framework/package dependencies. NewId generates
entity identifiers; it does not install a second message bus.

Multi-tenancy remains required. Plain contracts stay in Core; the Finbuckle/EF
adapter is in Persistence, and ingress/message authority is in Infrastructure.
Global-only and mixed models are supported without fabricated tenant identities.
See [multi-tenancy](multi-tenancy.md) and [shared runtime](shared-runtime.md).

## Runtime adapters

SharedKernel.Persistence now supplies scoped EF read/write behavior, bounded
repositories, transaction ownership, auditing/soft deletion, Finbuckle enforcement,
FusionCache/Redis and locks. SharedKernel.Infrastructure supplies host composition,
validated settings, HTTP/auth/gRPC and Wolverine/RabbitMQ integration.

The [migration audit](infrastructure-migration.md) records what was reused and why.
The [runtime guide](shared-runtime.md) documents registration and constraints.
Service entities, mappings, schemas, migration files and business policies remain
service-owned. Pricing, Licensing and Devices were not imported.

## Verification

Core unit tests cover equality, event collection/replay, pagination, credential
escaping/redaction, exception causes and tenant connection lookup. Infrastructure
unit tests cover ingress authority, error mapping, options, OAuth and gRPC client
credentials. Testcontainers suites exercise PostgreSQL, Redis and RabbitMQ,
including isolation, transactions, event delivery, retries and cache invalidation.
An HTTP test host exercises ordinary FastEndpoints, generated discovery and Scalar.

All projects use root warnings-as-errors and Nx project references. Static Wolverine
verification reruns exercised handlers without the runtime compiler package.
These checks do not establish full NativeAOT, other database providers or complete
application E2E. See [testing](testing.md) for required service-level proof.

## Aggregate boundaries

An aggregate root derives from BaseEntity<TId> (or implements narrower entity
contracts) and explicitly implements IAggregateRoot. BaseEntity alone does not make
an entity a root. For example, Order is a root and OrderLine is a child changed
through Order's business methods, not an independent write repository.

IGenericWriteRepository, GenericWriteRepository and AddRepositories require
IAggregateRoot at compile time. Read repositories remain available for roots,
children and projections; use AddReadRepository<TEntity, TId, TRead> when only a
read registration is needed. Aggregate methods enforce invariants and record domain
events. IUnitOfWork and Wolverine own persistence, transactions and durable delivery.
The marker does not itself enforce business rules or prevent direct DbContext writes;
service application code must preserve this boundary. Tenant isolation applies to
children as well as roots and is independent of the aggregate marker.

See [query specifications](specifications.md) for service-owned query placement,
unique reads, tracked aggregate loading and projection reuse through Ardalis.

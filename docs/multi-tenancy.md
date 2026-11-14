# Multi-tenancy

Multi-tenant support is required in the foundation. Individual services and their
features may be global, tenant-scoped or mixed. Tenant isolation is mandatory for
tenant-owned operations; global operations do not require a fabricated tenant.
Core contains ITenantScoped, operation scope and tenant connection contracts.
Persistence now supplies Finbuckle/EF enforcement with PostgreSQL isolation tests;
Infrastructure establishes trusted ingress/message scope. This document also states
requirements for future consuming services. See [shared runtime](shared-runtime.md).

## Service and entity scope

Do not add TenantId to every BaseEntity. Keep ITenantScoped for tenant ownership,
with explicit EF configuration and model checks. Global entities remain global.
A mixed service can contain both, with authorization and data boundaries defined
per operation rather than one service-wide switch that relaxes all enforcement.

Tenant-scoped access fails when its validated context is missing. Global access
uses an explicitly global context/repository path, never IgnoreQueryFilters on a
tenant repository. Separate context pairs may share a physical database when
global work must run without a tenant. Classify cache keys, messages and jobs the
same way; a missing tenant must not silently convert a tenant operation into a
global one. Global does not mean unauthenticated or publicly writable.

See [the persistence migration audit](infrastructure-migration.md) for the
shared project boundaries and global/tenant/mixed test matrix.

## Shared responsibilities

Carry forward the useful Finbuckle-based tenant resolution and EF integration
from Teck.Monorepo through focused adapters. Keep domain code independent of
Finbuckle, ASP.NET, EF and Wolverine. A plain tenant ID/ownership contract is
appropriate for tenant-owned entities; framework independence does not mean
removing multi-tenancy.

Keep tenant-ownership and future current-tenant contracts in SharedKernel.Core.
Finbuckle resolution belongs in an Infrastructure adapter, persistence enforcement
in the EF adapter, and message context propagation in the Wolverine adapter.
The broader Core import supersedes the earlier proposal for a separate tenant
abstractions project.

Do not copy the old tenant provisioning, billing/database strategy or HTTP-backed
tenant store automatically. Select the authoritative tenant directory and
provisioning workflow separately; internal directory access must follow the gRPC
service boundary. Tenant data isolation is required regardless of that choice.

## Identity and scope

- Resolve a tenant from authenticated, authorized context. A header, route value,
  message field or gRPC metadata value is a selection hint, not authorization.
- Verify tenant existence/status and the caller's right to act for it. Reject
  missing, unknown, disabled or mismatched tenants for tenant-scoped operations.
  Do not silently use a default tenant or manufacture an active tenant from its ID.
- Establish one immutable tenant scope before resolving tenant-sensitive contexts.
  Restore/dispose it after each request/message/job, including failures.
- Background jobs and durable message consumers must work without HttpContext.
  Concurrent tenants, retries and pooled objects must never inherit another scope.
- Classify global/platform data explicitly. Cross-tenant administration requires
  a separate authorized, audited path; normal repositories must not disable isolation.

## Database boundaries

Both read and write contexts enforce the same tenant ownership. Read-only database
permissions and NoTracking complement tenant isolation; neither replaces it.

For shared-database storage, require tenant filtering, trusted stamping on insert
and checks against cross-tenant update/delete. Include tenant scope in relevant
business uniqueness and relationship constraints. Test detached updates and
cross-tenant foreign-key references, not only normal tracked SaveChanges.

Prevent ordinary paths from bypassing tenant filters through raw SQL, bulk
operations or IgnoreQueryFilters. Where such operations are necessary, require
explicit tenant predicates/authorization and integration tests. Query filtering
alone is not a complete security boundary.

Support a clean adapter boundary for dedicated tenant databases without hardcoding
a database topology into entities. Shared versus dedicated/hybrid storage and
provisioning remain explicit per-service decisions, not claims of implemented
support. Resolve connections from trusted configuration; never from request data.
Service-owned migrations and supported-provider tests still apply.

The implemented shared-database profile uses **Finbuckle for EF row isolation**
and **Wolverine for native message TenantId, transactions and inbox/outbox**.
They receive one validated operation identity. Do not also enable Wolverine's
conjoined EF model customizer/stamping on these contexts.

This choice preserves ordinary scoped DI (including actor/clock dependencies),
explicit global operation paths and separate read/write database credentials.
Wolverine 6.64's conjoined builder requires an options-only constructor and builds
its contexts from the messaging database configuration. It is a different context
composition model, not a drop-in switch for this foundation. Native conjoined or
database-per-tenant profiles can be added when a service needs them, with dedicated
read-principal, provisioning, migration and generated-code tests. Neither profile
is claimed implemented here. Finbuckle HTTP tenant resolution is not additionally
registered: trusted claims plus the service-owned ITenantDirectory establish scope.

## Transport and other state

Set the validated tenant using InvokeForTenantAsync or DeliveryOptions.TenantId
at the entry point. Tenant middleware rejects absent envelope identity and payload
mismatches, and passes the identity into Finbuckle-backed persistence. Preserve it
across local domain-event handling, integration-event publication, durable
outbox/inbox delivery, retries and scheduled work. At the consumer, establish and
validate the scope before resolving persistence. A tenant value on an envelope
does not by itself authorize a publisher to act for that tenant.

Propagate tenant context on gRPC calls through a small client/server adapter.
The receiving service validates service identity and tenant authority; forwarded
metadata alone is insufficient. If a contract also contains TenantId, reject
disagreement with the validated execution context.

Tenant-scoped caches, object-storage keys, idempotency records and locks must
include tenant scope. Telemetry should identify the tenant without logging
credentials. Keep tenant context out of process-wide mutable singleton state.

## Required proof

Use two tenants with overlapping business identifiers:

- Verify a global-only service runs without tenant services, and a mixed service
  can use global reference data while rejecting unscoped tenant reads/writes.
- Unit tests cover resolution/authorization, missing and conflicting tenant data,
  and scope cleanup on success, exception and cancellation.
- Architecture tests enforce dependency boundaries and tenant-aware read/write
  access. They do not substitute for runtime isolation tests.
- Testcontainers tests prove read/write isolation, insert stamping, detached
  writes, constraints, cache/idempotency separation and parallel tenant scopes.
- Database/RabbitMQ tests prove tenant propagation through local translation,
  durable publication, broker restart, redelivery and background processing.
- Actual gRPC tests cover accepted and forged/mismatched tenant context.
- Aspire E2E verifies that a user in tenant A cannot view or mutate tenant B's
  resources through the gateway and downstream services.

## References

Finbuckle documents shared, dedicated and hybrid database approaches and EF tenant
validation in [its EF Core guide](https://www.finbuckle.com/MultiTenant/Docs/v10.1.6/EFCore).
Wolverine documents its tenant-aware EF/message integration in
[its multi-tenancy guide](https://wolverinefx.net/guide/durability/efcore/multi-tenancy).
These capabilities must be verified against our package pins; they are not proof
that the new repository already implements them.

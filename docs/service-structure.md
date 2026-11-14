# .NET service structure

This is the service baseline carried forward from Teck.Monorepo. It defines the
shape for service migrations into Teck.Cloud. The shared Core, Persistence and
Infrastructure libraries now have unit/integration and shared architecture tests.
Concrete business services and their service-specific architecture suites are not
yet created. Business scope remains a separate decision. Gateway and stateless
worker exceptions are described below. Add scoped layer instructions with each
real service project using [the agent instruction checklist](agent-instructions.md#new-service-layers).

## Layout

A stateful business service normally has three projects under
src/services/<domain>/<service>/:

```text
<Service>.Domain/
  Entities/
  ValueObjects/
  DomainEvents/
  Services/                         # Domain logic when needed
  <Service>.Domain.csproj
<Service>.Application/
  <Capability>/
    Features/<UseCase>/V1/
      <UseCase>Command.cs            # Or Query / Request as appropriate
      <UseCase>Validator.cs
      <UseCase>Handler.cs
    Responses/
    ReadModels/                     # Query specifications / projections
    Mapping/
    EventHandlers/DomainEvents/
    EventHandlers/IntegrationEvents/
  Database/
    <Service>DbContextBase.cs
    <Service>DbContext.cs            # Write context, migration target
    Configurations/
  <Service>.Application.csproj
<Service>.Host/
  Endpoints/<Capability>/           # External HTTP adapters
  Grpc/                             # Internal synchronous service adapters
  Database/
    Migrations/
    <Service>ReadDbContext.cs
    <Service>ReadRepository.cs
    <Service>WriteRepository.cs
    <Service>PersistenceExtensions.cs
  Infrastructure/                   # External clients and runtime adapters
  Program.cs
  <Service>.Host.csproj
```

This is a placement guide, not a requirement to create empty folders or a file for
every possible type. Each feature belongs to a business capability; avoid a single
Application/Features folder collecting unrelated use cases. Add versions for
versioned contracts and use cases deliberately. A V1 directory does not itself
configure the externally exposed URL.

A service-local Directory.Build.props is optional. If added, import the parent
configuration explicitly: MSBuild otherwise stops at the nearest props file.

## Responsibilities and references

| Project     | Owns                                                                                         | Reference direction                                                                                               |
| ----------- | -------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------- |
| Domain      | Entities, value objects, invariants, domain events                                           | No Application/Host or infrastructure dependencies; focused framework-independent domain primitives may be shared |
| Application | Use cases, commands/queries, validation, responses, mappings and handlers                    | Its Domain and relevant shared contracts; never its Host or another service's internals                           |
| Host        | Process entrypoint, transports, auth, DI, configuration, persistence adapters and migrations | Application; Domain where persistence/composition genuinely requires it; hosting infrastructure                   |

The retained database-context placement means Application has EF Core/persistence
references. Do not describe this as a framework-free Application layer. Moving
those contexts into a separate Infrastructure project would be an architecture
change, not a prerequisite or an implicit part of this migration.

Service-to-service calls use gRPC synchronously and Wolverine/RabbitMQ asynchronously.
HTTP/REST is for external ingress only. Follow [events and communication](messaging.md).
Services do not
reference another service's Host/Application/Domain projects or access its database.
Shared contracts carry IDs and integration DTOs, not EF entities or navigations.
Gateways remain routing/auth components and do not acquire business-domain layers.
A stateless worker can omit Domain when it has no domain model.

## Feature and transport flow

Endpoint -> Application command/query -> handler -> domain/persistence -> response.

- Retain FastEndpoints for HTTP transport, Wolverine for command/query and message
  dispatch, FluentValidation for validation, Mapperly for generated mappings and
  Ardalis.Specification for query specifications when porting the matching feature.
  Selected packages are centrally pinned; add references in each actual consumer.
  See [build quality](dotnet-quality.md) for analyzer and ErrorOr conventions.
- Endpoints handle route, authorization, transport translation and response status.
  Business decisions, database operations and entity mapping belong behind them.
- Put use-case requests, commands/queries and validators beside the handler in
  Application/<Capability>/Features/<UseCase>/V1. Responses and mappings belong to
  the capability. An HTTP-only binding contract, if needed, must be distinguished
  explicitly from the use-case contract rather than duplicating business validation.
- Wolverine handlers use its supported method conventions; do not introduce a second
  mediator framework. Keep domain events distinct from public integration events.
- Keep Program.cs as composition. Host binds and validates typed options; handlers
  consume typed settings, not arbitrary IConfiguration lookups. Required credentials
  and security-critical settings must fail clearly if missing, not use fake defaults.

### HTTP endpoints and typed results

FastEndpoints is the selected HTTP framework. Use its standard Endpoint<TRequest,
TResponse> (or appropriate built-in variant), Configure and HandleAsync/ExecuteAsync
APIs. Do not recreate the legacy Teck AuthenticatedEndpoint/AdminEndpoint hierarchy
or introduce custom endpoint base classes. Express authorization with supported
policies, endpoint configuration and focused composition helpers; preserve tenant
and permission checks when porting features.

Keep vertical slices under Application/<Capability>/Features/<UseCase>/V1 and align
Host/Endpoints/<Capability>/<UseCase>/V1 with that use case. Endpoint classes own
HTTP binding and response mapping; Wolverine handlers own application behavior.
Use Wolverine for CQRS rather than adding FastEndpoints' command/event bus as a
second dispatch mechanism.

Command/query handlers return typed success/error results (retain ErrorOr<T> when
porting the existing convention). Expected validation, not-found and conflict
outcomes are results; unexpected failures and cancellation retain their normal
exception semantics. Application results contain no HTTP IResult/status codes.
Invoke Wolverine with the full result type and map it at the HTTP boundary. Verify
both success and failure paths so dispatch does not discard errors or publish a
result as an unintended cascading message. Broker consumers retain their explicit
acknowledgement/retry contract rather than blindly adopting request/response results.

### Registration and source generation

Use ServiceScan.SourceGenerator for application service/repository/validator DI
registration with explicit assembly/type filters and lifetimes. Use its supported
GenerateServiceRegistrations and ScanForTypes APIs; the legacy [ServiceScan]
examples are not the supported pattern. Central version declarations alone do not
install or activate the generator. Verify generated registrations and scoped
context/unit-of-work ownership with the first consuming service.

FastEndpoints.Generator owns endpoint/type discovery and the supported generated
serialization/binding setup. ServiceScan must not register endpoints a second time.
Wolverine owns its handler discovery/code generation. Prefer generated or explicit
registration over new runtime assembly scanning; do not globally scan every class.

### API documentation

Use Scalar.AspNetCore for the interactive API reference. Scalar renders the OpenAPI
document; it does not generate it. Use the supported FastEndpoints OpenAPI
integration, documenting routes, versions, auth, success schemas and error responses.
Validate the generated document against actual endpoints with the first service.
Scalar is the selected UI; do not add a separate Swagger UI by default.

Treat OpenAPI generation and rendering as separate concerns for AOT readiness.
Do not infer that an OpenAPI generator is AOT-compatible because Scalar can render
its output. If the selected generator needs a JIT host, it can produce a versioned
static document during the build for a future AOT deployment; validate that path
before adopting it.

### Native AOT readiness and performance

Run with JIT while retaining EF Core. Aim to minimize future Native AOT migration
work; successful Native AOT publication of the entire service is not currently a
requirement. Keep useful EF features and do not restrict queries solely around its
experimental AOT support.

Prefer FastEndpoints' documented source-generation setup, System.Text.Json source
generation, Mapperly/direct mapping and LoggerMessage generation. Configure the
selected Wolverine version's supported build-time code generation and static
loading (TypeLoadMode.Static), verifying the generated handlers and the actual
messaging integrations. Wolverine has a documented AOT path; EF limitations do
not make Wolverine an assumed blocker.

Avoid introducing runtime code generation, dynamic loading or reflection-heavy
request paths. Track trimming/AOT diagnostics as compatibility evidence, without
blanket suppression or claiming compatibility before testing. Measure startup,
memory, allocations and request latency/throughput; removing one-time startup
reflection is not automatically a throughput improvement. Add representative
published-binary tests when enabling AOT for a service.

References:

- https://fast-endpoints.com/docs/native-aot
- https://www.nuget.org/packages/ServiceScan.SourceGenerator/
- https://scalar.com/products/api-references/integrations/aspnetcore/integration
- https://wolverinefx.net/guide/aot
- https://learn.microsoft.com/en-us/ef/core/performance/nativeaot-and-precompiled-queries

## Persistence and transactions

Retain PostgreSQL and EF Core for the service's persistence baseline. The existing
three-context pattern defines the entity model once in Application's abstract base,
with a tracked write context and a separate Host read context using NoTracking.
The read context derives from the base, not from the write context. Read/write
contexts do not imply separate databases or an available read replica.

Use the retained generic read/write repository and unit-of-work boundary in
handlers. Do not inject DbContext or Ardalis IRepositoryBase directly into them.
Write repositories track mutations in the same scoped write context. Under Wolverine
transaction middleware, it owns the persistence/outbox completion; the unit-of-work
adapter participates in that boundary instead of making an independent early commit.
Outside that pipeline, use the supported scoped EF outbox unit of work. Do not create per-entity
repository classes merely to wrap those generic operations. For load-then-mutate,
ensure the entity is tracked; for queries, use the read side and bounded projections.

When state changes require an outgoing event, prove that persistence and durable
message recording share the intended transaction/outbox. SaveChanges followed by
Publish is not, by itself, evidence of atomic delivery. Test retries, duplicate
consumption and interrupted delivery where the feature depends on them. Do not
silently switch a broker-required workflow into local-only behavior.

Support global, tenant-scoped and mixed services; follow [the tenant contract](multi-tenancy.md).
Tenant-owned data carries an explicit tenant boundary through HTTP, gRPC, messages,
queries and writes. Validate the trusted identity/context; a caller-supplied header
alone is not authority. Test cross-tenant rejection with real persistence. The
old repo's exact tenant provisioning/database strategy is not automatically adopted
by this structural baseline and must be assessed with the owning feature.

## CQRS and the read/write contract

The existing monorepo uses Wolverine as the in-process command/query dispatcher
and as its asynchronous messaging runtime. These are distinct responsibilities:

- An endpoint invokes a command/query through IMessageBus.InvokeAsync.
- A command handler uses the write repository within the shared transaction scope.
- A query handler uses the read repository and returns a response/projection.
- Cross-service integration events use Wolverine/RabbitMQ; synchronous service
  calls use gRPC. Local command/query dispatch stays inside the service.
- Domain events implement IDomainEvent and remain local; a local handler may translate
  them into separate IIntegrationEvent contracts. See [messaging](messaging.md).

CQRS here means separate command/query behavior and persistence access paths.
It does not, by itself, mean event sourcing, separate databases, read replicas,
or an independently projected read schema. Catalog is the verified example:
CreateProductHandler consumes IGenericWriteRepository and IUnitOfWork;
GetProductHandler consumes IGenericReadRepository.

For new stateful services, require these invariants regardless of whether
persistence remains in three projects or moves into a fourth:

1. Distinct read and write DbContext types share one authoritative entity model.
   The read context derives from the common base, not the write context. Existing
   names such as CatalogDbContext denote the write side; new names should make
   Read/Write explicit.
2. Read repositories resolve only the read context, with NoTracking by default.
   Query handlers must not receive a write repository or unit of work.
3. Write repositories resolve the tracked write context; command handlers may
   read through that same context when loading data to mutate or when consistency
   requires it. One transaction owner completes that same scoped context and outbox;
   a unit-of-work adapter must not independently commit ahead of Wolverine middleware.
4. NoTracking is a query tracking setting, not write authorization. Guard every
   SaveChanges/SaveChangesAsync entry point on the read context and use a
   read-only database principal for the read connection. Test direct SQL/bulk-write
   paths against that principal too; SaveChanges guards do not cover those paths.
5. Read and write may initially use the same database server with different
   credentials. A read replica is optional; if enabled, specify replica-lag and
   read-after-write behavior rather than assuming immediate visibility.
6. The write model owns schema migrations. The read context must not generate
   or apply a second competing set for the same schema. Provider-specific sets
   are different from read/write sets.
7. Enforce wiring and handler dependencies with architecture/DI tests. Use real
   Testcontainers integration tests to verify read-only restrictions, no tracking,
   successful write commits, isolation, migrations and the outbox boundary.

The inspected legacy CatalogReadDbContext only configures NoTracking; it does not
implement the write guards above. These are requirements for the new service
implementation. Teck.Cloud now tests these guards in ScopedDbContext and a real
PostgreSQL read principal; each service must prove its own registration/credentials.

## Multiple providers and the persistence-project decision

PostgreSQL is the inherited implementation, not a permanent PostgreSQL-only rule.
The intended portability targets are PostgreSQL, SQL Server, MySQL and MariaDB.
No additional provider is installed or claimed supported until its real tests pass.

Use shared SharedKernel.Persistence for reusable EF/cache adapters. Keep the
service's Domain/Application/Host layout: concrete mappings and migrations remain
service-owned. A separate service Persistence project is optional only when real
provider isolation or dependency constraints justify it; it is not the default.

EF Core scaffolds for the active provider. Maintain provider-specific migrations
and model snapshots; regenerate the relevant sets for model changes. Microsoft
documents two approaches: separate context types per provider, or a common context
type with separate migration assemblies. Merely selecting different output folders
for the same context/assembly is not a complete isolation strategy.

If we keep one logical write context, provider-specific migration projects are a
clear option, but they add projects beyond Domain/Application/Host. Alternatively,
provider-specific write context types can keep migrations inside the service Host.
Select that trade-off before scaffolding the first migration.
The read side shares the model and does not add separate schema migration sets.

Provider support includes SQL translation, types, collations, identity/concurrency,
indexes, transactions and durable messaging, not only an EF UseXxx switch. Test
MySQL and MariaDB separately even when they share a provider package. Pin compatible
EF/provider versions; do not infer compatibility from the target .NET runtime.
Wolverine documents PostgreSQL, SQL Server and MySQL/MariaDB persistence support,
but each chosen version and transaction/outbox wiring must be verified together.

References:

- https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/providers
- https://learn.microsoft.com/en-us/ef/core/querying/tracking
- https://learn.microsoft.com/en-us/ef/core/providers/
- https://wolverinefx.net/guide/durability/efcore/
- https://wolverinefx.net/guide/durability/mysql

## Migrations, hosting and deployment

Retain the same Host image with a bounded --migrate mode, with generated EF
migrations under Host/Database/Migrations and the write context as migration target.
The normal server mode and migration mode use the same candidate version. Migration
failure must stop rollout/startup. Choose the init-container/job execution strategy
with the deployment, including coordination when multiple replicas start. Re-running
migrations and concurrent migration execution need real verification; changing the
image tag does not reverse a database migration.

Add service defaults for health/readiness and telemetry, explicit startup wiring,
required secret/config injection, and graceful shutdown with the actual Host.
Use the supported Wolverine code-generation step when the chosen build/runtime
configuration requires it; validate the produced image rather than copying an
unverified legacy build command.

## Nx, tests and release unit

Each csproj is an Nx project, with ProjectReference dependencies and solution
registration. Domain/Application are dependencies of the Host, not three separate
service deployments. The deployable Host/image owns container:build and its release
identity. Shared-code changes must select affected Host consumers. Include shared
build configuration, external image inputs and deployment manifests where relevant.

The service normally adds:

- tests/unit/<Service>.UnitTests for domain and handler behavior.
- tests/integration/<Service>.IntegrationTests using Testcontainers and optionally
  WebApplicationFactory for external HTTP, covering migrations, gRPC, committed data
  and real RabbitMQ messaging. See [messaging proof](messaging.md#required-proof-with-the-first-service).
- tests/architecture/<Service>.Architecture.UnitTests enforcing the actual declared
  reference and placement rules, including forbidden handler persistence dependencies.

Assembled Aspire E2E and Aspire/KIND deployment tests live in the existing E2E lanes;
see [testing](testing.md). Architecture rules are documented here but not yet
mechanically enforced in Teck.Cloud. Add behavioral and architecture evidence with
the first service migration; do not claim enforcement from tags or prose alone.

## Legacy inconsistencies to resolve during migration

The inspected sources were Teck.Monorepo's src/services/AGENTS.md, commerce/AGENTS.md,
Catalog project files, Catalog.Application/Database, Catalog.Host/Endpoints and
Catalog.Architecture.UnitTests. They are evidence of intent/implementation, not
instructions to copy the entire repository.

- Legacy text says Application references Domain only, while actual Application
  projects reference shared persistence infrastructure and contain EF contexts.
- Legacy placement guidance puts request/validator types in Application, while
  Catalog currently has HTTP request/validator files under Host/Endpoints.
- Some architecture tests prohibit every Host-to-Domain dependency even though
  Host persistence adapters and project references use Domain entities.
- Legacy messaging prose claims atomic commits while also showing post-save
  publication; the actual transaction boundary must be verified when ported.
- Old setup instructions and service inventories include stale or unimplemented
  items. Do not copy obsolete service lists, secrets, tenant rules or release groups.

For each migrated service, reconcile code and tests against this baseline and
record any deliberate exception with that service. Never copy contradictory tests
and then weaken them merely to obtain a green build.

## Application unit of work and migration ownership

IUnitOfWork exposes SaveChangesAsync for application code. Wolverine-backed service
write contexts derive from WriteDbContext and implement that interface directly;
UseFoundationMessaging<TWrite> registers the same scoped context and Wolverine's
native WithDbContextAbstraction mapping. Repositories must share it. The handler
may save, but Wolverine's Eager middleware owns final transaction/outbox completion.
See [shared runtime](shared-runtime.md) for saves outside handlers and retry rules.

Keep EF migration files service-owned. Do not enable
UseEntityFrameworkCoreWolverineManagedMigrations for the same application tables: it
is an alternative schema owner, not another required layer. Wolverine's durable
messaging tables still require provisioned schema, coordinated in the service's
migration/deployment stage with appropriate privileges. Read credentials must not
run either schema setup. Test-only EnsureCreated/AutoCreate is not production
migration policy.

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

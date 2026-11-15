# .NET service structure

This is the service baseline carried forward from Teck.Monorepo. It defines the
shape for service migrations into Teck.Cloud. The shared Core, Persistence and
Infrastructure libraries now have unit/integration and shared architecture tests.
Concrete business services and their service-specific architecture suites are not
yet created. Business scope remains a separate decision. Gateway and stateless
worker exceptions are described below. Add scoped layer instructions with each
real service project using [the agent instruction checklist](agent-instructions.md#new-service-layers).

## Layout

A business service has four projects under
src/services/<domain>/<service>/:

```text
<Service>.Domain/
  Aggregates/
    <Aggregate>/
      <Aggregate>.cs              # Root owns business invariants
      <ChildEntity>.cs
      <Aggregate>Errors.cs        # Domain-owned ErrorOr errors
      ValueObjects/
      DomainEvents/               # Local domain events
      Services/                   # Optional pure domain calculations
  Common/                         # Only concepts shared across aggregates
  <Service>.Domain.csproj
<Service>.Application/
  <Capability>/
    Features/<UseCase>/V1/
      <UseCase>Command.cs         # Or Query as appropriate
      <UseCase>Validator.cs
      <UseCase>Handler.cs
    Responses/
    ReadModels/                   # Query-specific projection types
    Mapping/                      # Mapperly mappers
    EventHandlers/DomainEvents/
    EventHandlers/IntegrationEvents/
  <Service>.Application.csproj
<Service>.Infrastructure/
  Database/
    <Service>WriteDbContext.cs    # Tracked write context
    <Service>ReadDbContext.cs     # Untracked, save-guarded read context
    PersistenceExtensions.cs
    Configurations/
      Shared/                     # Mappings present on both sides
      Write/                      # Write aggregate mappings
      Read/                       # Read projection mappings
    Migrations/                   # Write-context schema history
  Clients/                        # Outbound gRPC/other service adapters
  Caching/                        # Only when service-specific
  Messaging/                      # Only when service-specific
  <Service>.Infrastructure.csproj
<Service>.Host/
  Endpoints/<Capability>/<UseCase>/V1/
  Grpc/                           # Inbound synchronous service adapters
  Composition/
  Program.cs
  <Service>.Host.csproj
```
This is a placement guide, not a requirement to create empty folders or a file for
every possible type. Each feature belongs to a business capability; avoid a single
Application/Features folder collecting unrelated use cases. Keep each command or
query, validator and handler together under
Application/<Capability>/Features/<UseCase>/V<n>/. The V1 shown above is an example;
versions are added deliberately for versioned contracts and use cases. A version
directory does not itself configure the externally exposed URL.

Capability-level Responses, ReadModels, Mapping and EventHandlers folders are also
allowed in Application. Application-root C# files are limited to
DependencyInjection.cs, ApplicationServiceCollectionExtensions.cs,
ServiceCollectionExtensions.cs, ApplicationComposition.cs, GlobalUsings.cs and
AssemblyInfo.cs; put other source under a capability or feature.

A service-local Directory.Build.props is optional. If added, import the parent
configuration explicitly: MSBuild otherwise stops at the nearest props file.

## Responsibilities and references

| Project        | Owns                                                                                   | Reference direction                                                       |
| -------------- | -------------------------------------------------------------------------------------- | ------------------------------------------------------------------------- |
| Domain         | Aggregates, value objects, invariants, domain errors and local events                  | Shared domain contracts only; no Application, Infrastructure or Host      |
| Application    | Use cases, commands/queries, validators, DTOs, Mapperly mappings and handlers          | Domain and shared contracts; no Infrastructure or Host                    |
| Infrastructure | Read/write EF contexts, configurations, migrations, repositories and outbound adapters | Application, Domain and relevant shared adapters                          |
| Host           | Process entrypoint, inbound HTTP/gRPC, auth, configuration and composition             | Application and Infrastructure; Domain only where composition requires it |

This follows the inward dependency rule used by Clean Architecture and Onion Architecture:
Domain is at the center, Application owns use cases and ports, Infrastructure implements
outward-facing adapters, and Host is the composition root and inbound transport. The
four-project layout keeps EF Core and provider packages in the service's Infrastructure
project. Application handlers consume repository, cache and unit-of-work contracts
without referencing concrete contexts. Host composes the service and is the only
deployable process/image. SharedKernel.Persistence and SharedKernel.Infrastructure
remain reusable libraries, not additional service layers.
Service-to-service calls use gRPC synchronously and Wolverine/RabbitMQ asynchronously.
HTTP/REST is for external ingress only. Follow [events and communication](messaging.md).
Services do not
reference another service's Domain/Application/Infrastructure/Host projects or access
its database.
Shared contracts carry IDs and integration DTOs, not EF entities or navigations.
Gateways remain routing/auth components and do not acquire business-domain layers.
A stateless worker may omit Domain only when its service-root service-layout.json
declares {"kind":"stateless-worker"}. Do not infer this exception from the service
name; without the marker, the four-project business-service layout applies.

## Aggregate-first Domain and errors

Group domain types by aggregate rather than by technical type. Domain source
belongs under Aggregates/<Aggregate>/ or Common/; do not create top-level Entities, ValueObjects, DomainEvents, Errors or Services
folders. For example, Domain/Aggregates/Order/
holds the Order root, child entities such as OrderLine, OrderErrors, related value
objects, local events and any pure domain service. Do not create empty folders or
one folder per child entity. Keep genuinely shared domain concepts under Common/
only when multiple aggregates own them.

Aggregate and value-object factories enforce their own invariants, including
required values, maximum string lengths and valid quantities. Return ErrorOr<T>
for expected creation or mutation failures, using stable domain error codes defined
beside the owning aggregate (for example, OrderErrors.NameTooLong). A factory
must reject an overlong name before constructing the value or aggregate; request
and handler validators may give earlier feedback, but they do not own or replace
the Domain invariant. Guard constructors so callers cannot bypass the factory.
Application owns orchestration errors such as a requested ID not being found or a
downstream capability being unavailable. Host maps the full ErrorOr result to
transport status; it does not define business errors. This is the chosen Teck.Cloud
convention, not a claim that Monorepo already centralized domain errors.

Use Ardalis.SmartEnum for finite domain choices that need named instances,
behavior or domain metadata. Keep a type with its owning aggregate; place it in
Domain/Common/ only when it is truly shared across aggregates. A plain enum is
sufficient for a simple closed set with no behavior. Add the SmartEnum package
reference to the Domain project only when a real domain type uses it, and manage
its version centrally under the repository package policy.

A domain service is a pure business calculation that does not naturally belong
on one entity or value object. Monorepo's OrderPricingService sums order lines;
it does not call the separate Pricing service. Cross-service calls and repository
lookups belong outside Domain. Use a service only when a method on the aggregate
or value object would be the worse owner.

## Feature and transport flow

Endpoint -> Application command/query -> handler -> domain/persistence -> response.

- Retain FastEndpoints for HTTP transport, Wolverine for command/query and message
  dispatch, FluentValidation for validation, Mapperly for generated mappings and
  Ardalis.Specification for query specifications when porting the matching feature.
  Selected packages are centrally pinned; add references in each actual consumer.
  See [validation boundaries](validation.md) for HTTP and Wolverine validator behavior,
  and [build quality](dotnet-quality.md) for analyzer and ErrorOr conventions.
- Endpoints handle route, authorization, transport translation and response status.
  Business decisions and database operations belong behind them. Use Mapperly-generated
  mappers for mappings between aggregates, Application DTO/read models and Host request/
  response contracts; direct assignment of one scalar value is fine. Do not use AutoMapper
  or handwritten mapper classes. Put application mappings under Application/<Capability>/Mapping
  and transport mappings in a Host-owned mapper near the endpoint. Name each mapper type
  with the Mapper suffix and mark it with the [Mapper] attribute. The service-layout
  verifier checks authored C# Mapper-suffixed types in Application/Host for that attribute;
  the shared architecture suite rejects AutoMapper assembly references. These checks do not
  classify arbitrary inline object initializers, endpoint/handler expressions or LINQ
  projections, so keep transformations in named mapper types and test their behavior.
- Put use-case requests, commands/queries, validators and handlers together in
  Application/<Capability>/Features/<UseCase>/V<n>. Responses and mappings belong to
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

Keep vertical slices under Application/<Capability>/Features/<UseCase>/V<n> and align
Host/Endpoints/<Capability>/<UseCase>/V<n> with that use case. Endpoint classes own
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
generation, Mapperly-generated mappings and LoggerMessage generation. Configure the
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

Retain PostgreSQL and EF Core as the current persistence baseline. Each service owns
distinct read and write DbContext types in Infrastructure: the tracked write context
derives from the shared WriteDbContext, and the untracked, save-guarded read context
derives directly from ScopedDbContext. Keep their EF configurations and migrations in
Infrastructure; do not add a service-specific abstract context base in Application.
Separate context types do not imply separate databases or an available read replica.

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

For new stateful services, require these invariants in the four-project layout:

1. Distinct read and write DbContext types use the same shared entity mappings,
   while each can include its own model. The write context derives from
   SharedKernel.Persistence.Database.WriteDbContext (Wolverine unit of work and
   outbox); the read context derives directly from ScopedDbContext with
   `readOnly: true`. They cannot derive from one service DbContext base class.
   Keep EF configurations in Infrastructure/Database/Configurations/Shared,
   Write and Read. Implement ISharedModelConfiguration<TEntity>,
   IWriteModelConfiguration<TEntity> or IReadModelConfiguration<TEntity>.
   Both contexts scan the Infrastructure assembly with their respective
   ModelConfigurationFilters predicate **before** calling base.OnModelCreating.
   The base applies tenant and soft-delete filters to all discovered entities.
   Only the write context owns migrations; a read-only view/projection belongs
   only in the read model. For a genuinely small read model, mark the rich
   Product mapping as IWriteModelConfiguration<Product> and the separate
   GetProductByIdReadModel mapping as IReadModelConfiguration<GetProductByIdReadModel>; the
   Shared marker is only for mappings both contexts actually need. Name each
   read-side EF type with a ReadModel suffix, such as OrderReadModel for a reused
   shape or GetOrderByIdReadModel for a query-specific shape. This is separate
   from an external HTTP response type. A read
   projection still needs tenant and soft-delete filtering; verify that its
   mapped fields and query filters enforce those boundaries.

   ```csharp
   // Infrastructure/Database/CatalogWriteDbContext.cs
   public sealed class CatalogWriteDbContext(
       DbContextOptions<CatalogWriteDbContext> options,
       OperationContext operation,
       IDbContextOutbox outbox,
       IMessageContext messaging)
       : WriteDbContext(options, operation, outbox, messaging)
   {
       public DbSet<Product> Products => Set<Product>();

       protected override void OnModelCreating(ModelBuilder modelBuilder)
       {
           modelBuilder.ApplyConfigurationsFromAssembly(
               typeof(CatalogWriteDbContext).Assembly,
               ModelConfigurationFilters.WriteConfigurationsFilter);
           base.OnModelCreating(modelBuilder);
       }
   }

   // Infrastructure/Database/CatalogReadDbContext.cs
   public sealed class CatalogReadDbContext(
       DbContextOptions<CatalogReadDbContext> options,
       OperationContext operation)
       : ScopedDbContext(options, operation, readOnly: true)
   {
       public DbSet<GetProductByIdReadModel> ProductSummaries => Set<GetProductByIdReadModel>();

       protected override void OnModelCreating(ModelBuilder modelBuilder)
       {
           modelBuilder.ApplyConfigurationsFromAssembly(
               typeof(CatalogWriteDbContext).Assembly,
               ModelConfigurationFilters.ReadConfigurationsFilter);
           base.OnModelCreating(modelBuilder);
       }
   }
   ```

2. Read repositories resolve only the read context, with NoTracking by default.
   Query handlers must not receive a write repository or unit of work.
   Every command handler injects a write repository; every query handler injects
   a read repository or IReadCacheService<TModel, TId>. Cache loading must be
   registered against an untracked, tenant-scoped read path and verified in a
   service integration test; the architecture rule cannot prove loader wiring.
3. Write repositories resolve the tracked write context; command handlers may
   read through that same context when loading data to mutate or when consistency
   requires it. One transaction owner completes that same scoped context and outbox;
   a unit-of-work adapter must not independently commit ahead of Wolverine middleware.
4. NoTracking is a query tracking setting, not write authorization. Guard every
   SaveChanges/SaveChangesAsync entry point on the read context and use a
   read-only database principal when a separate read connection is configured.
   Test direct SQL/bulk-write paths against that principal too; SaveChanges guards
   do not cover those paths. If the read connection string is absent, use the write
   connection string as Monorepo does; the read context still rejects SaveChanges,
   but the shared database principal does not make raw SQL read-only.
5. A read connection string is optional and falls back to the required write
   connection string. A dedicated read principal or read replica is optional;
   if a replica is enabled, specify replica-lag and read-after-write behavior
   rather than assuming immediate visibility.
6. The write model owns schema migrations. Target the service write DbContext
   for EF scaffolding, and store migrations in the service Infrastructure assembly.
   This differs from Monorepo's current layout. The read context must not generate or
   apply a competing migration set for the same schema. Provider-specific sets are
   different from read/write sets.
7. Enforce wiring and handler dependencies with architecture/DI tests. Use real
   Testcontainers integration tests to verify read-only restrictions, no tracking,
   successful write commits, isolation, migrations and the outbox boundary.

The inspected legacy CatalogReadDbContext only configures NoTracking; it does not
implement the write guards above. These are requirements for the new service
implementation. Teck.Cloud now tests these guards in ScopedDbContext and a real
PostgreSQL read principal; each service must prove its own registration/credentials.

## Multiple providers and Infrastructure ownership

PostgreSQL is the inherited implementation, not a permanent PostgreSQL-only rule.
The intended portability targets are PostgreSQL, SQL Server, MySQL and MariaDB.
No additional provider is installed or claimed supported until its real tests pass.

The four service projects are Domain, Application, Infrastructure and Host. Keep
concrete contexts, mappings and migrations service-owned in Infrastructure. Do not add
a fifth service-specific Persistence project by default. If a real provider-isolation
or dependency constraint requires another project, document the exception, its project
references and the provider-specific verification before adding it.

EF Core scaffolds for the active provider. Maintain provider-specific migrations
and model snapshots; regenerate the relevant sets for model changes. Microsoft
documents two approaches: separate context types per provider, or a common context
type with separate migration assemblies. Merely selecting different output folders
for the same context/assembly is not a complete isolation strategy.

If we keep one logical write context, provider-specific migration projects are a
clear option, but they add projects beyond the four-layer baseline. Alternatively,
provider-specific write context types can keep migrations inside service Infrastructure.
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
migrations under Infrastructure/Database/Migrations and the write context as migration target.
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
registration. Host composes Application and Infrastructure; Infrastructure implements
Application ports and references Domain. These projects make one service, not separate
service deployments. The deployable Host/image owns container:build and its release
identity. Shared-code changes must select affected Host consumers. Include shared
build configuration, external image inputs and deployment manifests where relevant.

The service normally adds:

- tests/unit/<Service>.UnitTests for domain and handler behavior.
- tests/integration/<Service>.IntegrationTests using Testcontainers and optionally
  WebApplicationFactory for external HTTP, covering migrations, gRPC, committed data
  and real RabbitMQ messaging. See [messaging proof](messaging.md#required-proof-with-the-first-service).
- tests/architecture/<Service>.Architecture.UnitTests is required for a stateful
  business service. It must contain an authored test and direct ProjectReferences to
  the four service projects plus tests/architecture/Teck.Cloud.ArchitectureRules.

Assembled Aspire E2E and Aspire/KIND deployment tests live in the existing E2E lanes;
see [testing](testing.md). The reusable ArchUnitNET rule library under
tests/architecture/Teck.Cloud.ArchitectureRules ports the applicable Monorepo CQRS,
domain, aggregate, domain-event and validator rules. The shared architecture suite
proves that these rules reject compiled bad fixtures. A stateful service architecture
test loads its Domain, Application, Infrastructure and Host assemblies with ArchLoader,
then calls SharedArchitectureRules.AssertAll(architecture, domainAssembly,
applicationAssembly, infrastructureAssembly, hostAssembly, domainNamespace,
applicationNamespace). No service-specific rule runs until that service and its
test suite exist; runtime behavior still requires integration evidence.

The only service layout exception is a service-root service-layout.json whose full
contents are exactly {"kind":"stateless-worker"}. It requires Application,
Infrastructure and Host, permits Domain to be absent, and skips the stateful-service
architecture-suite requirement. Unknown or malformed markers fail the layout check;
a service name alone never selects this exception.

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

IGenericWriteRepository, GenericWriteRepository, AddWriteRepository and AddRepositories require
IAggregateRoot at compile time. Read repositories remain available for roots,
children and projections; use AddReadRepository<TEntity, TId, TRead> when only a
read registration is needed. Aggregate methods enforce invariants and record domain
events. IUnitOfWork and Wolverine own persistence, transactions and durable delivery.
The marker does not itself enforce business rules or prevent direct DbContext writes;
service application code must preserve this boundary. Tenant isolation applies to
children as well as roots and is independent of the aggregate marker.

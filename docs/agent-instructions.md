# Scoped agent instructions

## How to use the hierarchy

Start with root AGENTS.md. Read ancestor instructions and the closest instructions
for the files being changed. Read another area's file when the task crosses that
boundary, such as implementing a repository and its integration tests.

Follow documentation links for the responsibility at hand. A list of links is a
routing map, not an instruction to load every page and all of its references.
Read enough of the canonical contract to implement the change correctly; context
saving must not hide relevant tenant, transaction, authentication or test rules.

This layout avoids putting all detail in root instructions. Tools differ in how
they discover nested instructions; agents that do not load them automatically must
follow the root routing map explicitly. Instructions guide implementation; compiler,
analyzer, architecture, behavioral and CI checks provide enforcement.

## Verify a contract before claiming it works

For an answer or implementation that depends on existing behavior, inspect the
actual definitions first. Follow inherited interfaces/base classes, call sites,
DI and configuration entry points, and the test that exercises the behavior.
Read the corresponding Monorepo implementation and its migration decision when
porting a foundation feature. Do not infer an API from a class name or a passing
fixture test.

For a change, write down the affected contract and the smallest test that could
falsify it. Include the alternate path that matters: tenant/global scope,
read/write registration, optional connection fallback, soft/hard deletion,
rollback, or cache miss. Run a real boundary test where storage or transport is
part of the claim. Architecture fixtures prove that a rule detects examples;
they do not prove a service calls the rule or wires the correct runtime.

Before reporting completion, compare implementation, registration, documentation
and tests against each acceptance criterion. State which checks ran, their result,
and the exact behavior still unverified. An unrun integration or service test is
an open gap, even when the shared library builds.

## Existing boundaries

| Location                               | Responsibility                                         |
| -------------------------------------- | ------------------------------------------------------ |
| Root AGENTS.md                         | Tooling, safety, quality floor and task routing        |
| src/services                           | Service layout, slices and communication contract      |
| src/shared                             | Shared dependency direction and project routing        |
| src/shared/SharedKernel.Core           | Domain primitives and framework-neutral contracts      |
| src/shared/SharedKernel.Persistence    | EF, specifications, transactions, isolation and caches |
| src/shared/SharedKernel.Infrastructure | Auth/transports, messaging, health and observability   |
| src/apps and src/packages              | Web/mobile ownership and shared TypeScript boundaries  |
| src/aspire                             | AppHost profiles, local resources and lifecycle        |
| tests                                  | Test contract, common tooling and coverage             |
| tests/unit, integration, architecture  | Boundary-specific evidence                             |
| tests/e2e, then web or kubernetes      | Assembled/deployed behavior and resource ownership     |
| tests/fixtures                         | Test-only composition and substitutions                |
| deployment                             | Environment overlays and candidate manifests           |
| .github and tools                      | CI, supply-chain controls and repository automation    |
| docs                                   | Canonical documentation and instruction upkeep         |

## Canonical documents by concern

Read these when changing the corresponding behavior, rather than copying their
contents into more instruction files:

| Concern                                           | Source                                                                                                           |
| ------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------- |
| Layout, Nx graph and ownership                    | [architecture](architecture.md)                                                                                  |
| Domain/Application/Infrastructure/Host and slices | [service structure](service-structure.md)                                                                        |
| Shared contracts and actual wiring                | [foundation](shared-foundation.md), [runtime](shared-runtime.md), [migration audit](infrastructure-migration.md) |
| CQRS, domain/integration events, transactions     | [messaging](messaging.md), [runtime](shared-runtime.md)                                                          |
| Tenant authority and isolation                    | [multi-tenancy](multi-tenancy.md)                                                                                |
| Identity authentication and organization lookup   | [Keycloak composition](keycloak-authservices.md), [identity tenancy](identity-tenancy.md)                        |
| Read/write loading and projections                | [specifications](specifications.md)                                                                              |
| Redis, distributed/output caching                 | [caching](caching.md), [Sentinel](redis-sentinel.md)                                                             |
| OTEL, logs, health and redaction                  | [observability](observability.md), [health](health.md)                                                           |
| Test selection, mocks and data                    | [testing](testing.md), [tooling](test-tooling.md)                                                                |
| Analyzer/package/build enforcement                | [.NET quality](dotnet-quality.md), [coverage](coverage.md)                                                       |
| Aspire and deployed KIND tests                    | [local development](local-development.md)                                                                        |
| CI, provenance and deferred work                  | [CI](ci.md), [supply chain](supply-chain.md), [roadmap](ci-roadmap.md)                                           |

## New service layers

No concrete business-service projects exist yet. A stateful business service uses
Domain, Application, Infrastructure and Host. Add AGENTS.md in each real project
directory, adapt checklists to actual responsibilities, and link to canonical
documents. The sole layout exception is a service-root service-layout.json whose
full contents are exactly {"kind":"stateless-worker"}. It requires Application,
Infrastructure and Host, permits Domain to be absent, and skips the stateful
architecture-suite requirement. Unknown or malformed markers fail; never infer the
exception from a service name. For the normal
src/services/<domain>/<service>/<Service>.Domain layout, the docs prefix is
../../../../../docs/. Verify it after choosing the real paths.

A service-root file is useful only for service-specific contracts, ownership and
local commands. It should not repeat the parent service policy.

### Domain/AGENTS.md

- Group Domain source under Aggregates/<Aggregate>/ or Common/; do not create top-level Entities, ValueObjects, DomainEvents, Errors or Services folders.
- State this service's invariants and capability boundaries. Use explicit aggregate roots; mutate children through the root. Factories return ErrorOr for expected invariant failures, with stable errors beside the aggregate (including string-length limits).
- Use Ardalis.SmartEnum only for finite choices with domain behavior or metadata; use plain enums for simple choices. See [aggregate-first Domain guidance](service-structure.md#aggregate-first-domain-and-errors).
- Keep domain events local and framework-neutral; do not depend on Application, Infrastructure, Host, EF/transport adapters or another service's implementation.
- Link the domain/event and tenant contracts relevant to these entities.
- Point to focused unit tests and the service's architecture tests.

### Application/AGENTS.md

- Put each command/query, validator and handler together under <Capability>/Features/<UseCase>/V<n>; keep Responses, ReadModels, Mapping and EventHandlers capability-scoped. Root C# files are limited to the six names in the canonical guide.
- Use Wolverine CQRS and ErrorOr for application results. Domain owns invariant errors; Host owns transport status mapping.
- Query handlers use read repositories. Command handlers use tracked aggregate
  loading and the scoped IUnitOfWork; preserve Wolverine transaction ownership.
- Describe validated operation scope and local-to-integration event translation.
- Keep service EF contexts, mappings and migrations in Infrastructure. Handlers use application ports, not DbContext directly.
- Link specifications, messaging, tenancy and the relevant unit/integration suites.

### Infrastructure/AGENTS.md

- State the service-owned write/read contexts, EF configurations, migration target and provider-specific adapters.
- Keep handlers behind Application ports; infrastructure implements those ports and depends inward on Application/Domain.
- Link persistence, migration, tenancy and integration-test guidance.

### Host/AGENTS.md

- State actual transports, entrypoints and service composition.
- Use standard FastEndpoints/generated discovery, ServiceScan, generated JSON and
  Scalar. Keep external HTTP adapters thin and internal synchronous calls on gRPC.
- Wire trusted auth/scope, the distinct read/write contexts and native inbox/outbox.
- Keep concrete mappings and provider-specific migrations service-owned; document
  the candidate image's migration mode and failure behavior.
- Use shared health, OTEL/Serilog and Sentinel composition rather than duplicating it.
- Link actual integration, Aspire acceptance and deployment checks plus Nx targets.

Create these files with the first slice and verify their instructions against the
actual references, DI registrations and tests. Do not claim a provider, protocol,
permission boundary or architecture rule works merely because a file requires it.

## Maintenance

Keep root instructions small. Put a rule in the narrowest directory that owns it;
repeat only a critical boundary invariant when necessary to prevent misuse.
Prefer a short checklist and links over copied examples, package version tables,
service inventories or historical discussions.

For changed instructions:

1. Check their parent and affected children for conflicts.
2. Resolve every local Markdown link relative to its containing file.
3. Keep scope-specific links reachable from the root map.
4. Update stale canonical statements exposed by the change.
5. Run formatting checks. Runtime tests are needed when implementation changes,
   not simply because instruction prose changed.

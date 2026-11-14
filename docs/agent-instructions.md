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

| Concern                                       | Source                                                                 |
| --------------------------------------------- | ---------------------------------------------------------------------- |
| Layout, Nx graph and ownership                | [architecture](architecture.md)                                        |
| Domain/Application/Host and slices            | [service structure](service-structure.md)                              |
| Shared contracts and actual wiring            | [foundation](shared-foundation.md), [runtime](shared-runtime.md)       |
| CQRS, domain/integration events, transactions | [messaging](messaging.md), [runtime](shared-runtime.md)                |
| Tenant authority and isolation                | [multi-tenancy](multi-tenancy.md)                                      |
| Read/write loading and projections            | [specifications](specifications.md)                                    |
| Redis, distributed/output caching             | [caching](caching.md), [Sentinel](redis-sentinel.md)                   |
| OTEL, logs, health and redaction              | [observability](observability.md), [health](health.md)                 |
| Test selection, mocks and data                | [testing](testing.md), [tooling](test-tooling.md)                      |
| Analyzer/package/build enforcement            | [.NET quality](dotnet-quality.md), [coverage](coverage.md)             |
| Aspire and deployed KIND tests                | [local development](local-development.md)                              |
| CI, provenance and deferred work              | [CI](ci.md), [supply chain](supply-chain.md), [roadmap](ci-roadmap.md) |

## New service layers

No concrete service projects exist yet. When adding one, create AGENTS.md in each
real layer directory as part of the scaffold. Adapt these checklists to its actual
responsibilities and add relative links to the canonical documents. For the normal
src/services/<domain>/<service>/<Service>.Domain layout, the docs prefix is
../../../../../docs/. Verify it after choosing the real paths.

A service-root file is useful only for service-specific contracts, ownership and
local commands. It should not repeat the parent service policy. A gateway or
stateless worker does not need fictitious Domain/Application projects.

### Domain/AGENTS.md

- State the aggregates, invariants and capability boundaries owned by this service.
- Use explicit aggregate roots; mutate children through the root.
- Keep domain events local and framework-neutral; do not depend on Host,
  Application, EF/transport adapters or another service's implementation.
- Link the domain/event and tenant contracts relevant to these entities.
- Point to focused unit tests and the service's architecture tests.

### Application/AGENTS.md

- State the capability-based slice layout and the service's commands/queries.
- Use Wolverine CQRS and ErrorOr; transport status mapping belongs in Host.
- Query handlers use read repositories. Command handlers use tracked aggregate
  loading and the scoped IUnitOfWork; preserve Wolverine transaction ownership.
- Describe validated operation scope and local-to-integration event translation.
- Keep concrete EF model placement consistent with the chosen three-project
  baseline; handlers still do not consume DbContext directly.
- Link specifications, messaging, tenancy and the relevant unit/integration suites.

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

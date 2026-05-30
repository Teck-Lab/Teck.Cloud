# Teck.Cloud — Agent Instructions

.NET 10 microservices, clean architecture. Canonical rules: [`.github/copilot-instructions.md`](file:///workspaces/Infrastructure/Teck.Cloud/.github/copilot-instructions.md) + [`.github/instructions/`](file:///workspaces/Infrastructure/Teck.Cloud/.github/instructions). This file is a **codemap**, not a re-statement of the rules.

## Instruction Precedence

1. Existing source code and established patterns
2. `Directory.Build.props`, `Directory.Packages.props`, `global.json`, `nuget.config`, `stylecop.json`, `.editorconfig`
3. `.github/instructions/*.instructions.md`
4. Task-specific instructions

## Repository Layout

```
src/
├── aspire/
│   ├── Teck.Cloud.AppHost/           — .NET Aspire orchestrator (entry point for local dev)
│   ├── Teck.Cloud.ServiceDefaults/   — shared OpenTelemetry / health / resilience defaults
│   └── keycloak/                     — Keycloak Aspire integration
├── auth/                             — Keycloak realm export + Containerfile
├── buildingblocks/
│   ├── SharedKernel.Core/            — Caching, CQRS, Database, Devices, Domain, Events, Exceptions
│   ├── SharedKernel.Events/          — domain + integration event contracts
│   ├── SharedKernel.Grpc.Contracts/  — generated gRPC contracts (cross-service)
│   ├── SharedKernel.Infrastructure/  — EF Core, MassTransit, Outbox plumbing
│   └── SharedKernel.Persistence/     — persistence abstractions, multi-provider helpers
├── gateways/
│   ├── Web.Public.Gateway/           — YARP, public-facing (image: yarp-gateway)
│   ├── Web.Admin.Gateway/            — YARP, internal admin (image: admin-gateway)
│   ├── Web.Aggregate.Gateway/        — aggregation gateway (scaffold, not yet deployed)
│   └── Web.Edge/                     — edge gateway (scaffold, not yet deployed)
├── services/
│   ├── basket/    catalog/    customer/    device/
│   ├── location/  order/      product/
│   ├── statistic/                    — single-provider, no .Migrations.{MySql,PostgreSQL,SqlServer}
│   └── image-generator/              — stateless API, only .Api + .Application
└── migrations/                       — Teck.Cloud.Migrations runner + Containerfile

deployment/{service}/                 — production Dockerfiles per service
tests/
├── unit/{Service}.UnitTests/         — xUnit, no I/O
├── integration/{Service}.IntegrationTests/  — Testcontainers, real DB/MQ
└── architecture/{catalog,shared}/    — ArchUnitNET-style rules
tools/migrations  tools/scaffolding   — code generation + migration tooling
```

## Standard Service Skeleton

A "full" service (basket, catalog, customer, device, location, order, product) ships **5 projects**:

```
{service}/
├── {Service}.Domain/                          — entities, value objects, domain events
├── {Service}.Application/                     — handlers, requests, validators, read models
├── {Service}.Infrastructure/                  — EF Core DbContext, repositories, integrations
├── {Service}.Api/                             — minimal API endpoints (transport-thin)
└── Directory.Build.props
```

Migrations for all services are consolidated under `src/migrations/`:
- `Teck.Cloud.Migrations.PostgreSQL/` — PostgreSQL migrations for all services
- `Teck.Cloud.Migrations.SqlServer/` — SQL Server migrations for all services
- `Teck.Cloud.Migrations.MySql/` — MySQL migrations for all services

**Reduced shapes**:
- `statistic` — full Domain + Application + Infrastructure, but **no migration projects** (single DB provider).
- `image-generator` — only `.Api` + `.Application` (stateless, no aggregate state).
## Architecture Rules

- Clean architecture: Domain ← Application ← Infrastructure ← API
- API projects are transport-thin: endpoint classes only. No `*Request` / `*Validator` in API namespace.
- Application owns: feature handlers, `Request`, `Validator`, read models, repository interfaces.
- Infrastructure owns: persistence, adapters, external integrations.
- gRPC handlers folder path may be `Grpc/gRpc/V1` while C# namespaces remain `*.Grpc.V1`.

### Application Folder Convention

```
<Capability>/Features/<UseCase>/V1/   — handlers, Request, Validator
<Capability>/Responses/
<Capability>/ReadModels/
<Capability>/Repositories/
<Capability>/EventHandlers/DomainEvents/
```

Never use root-level `Application/Features/*` folders.

## C# Style

- .NET 10, `sealed` classes by default, records for immutable data
- Nullable reference types enabled and honored
- `CancellationToken` flows through all async chains
- No `async void`, `.Result`, `.Wait()`, `ContinueWith`
- Prefer `TryParse`/`TryGetValue` over exception-driven control flow
- Minimal, bounded changes — do not refactor unrelated code

## Testing

- xUnit, Arrange-Act-Assert, `Method_WhenCondition_ExpectedResult` naming
- Integration tests prefer real dependencies (Testcontainers)
- Architecture tests live in [`tests/architecture/`](file:///workspaces/Infrastructure/Teck.Cloud/tests/architecture) — enforce layer boundaries
- Add / adjust tests with every behavior change

## Skills (read `SKILL.md` before proceeding)

| Trigger | File |
|---------|------|
| xUnit tests | [`.github/skills/csharp-xunit/SKILL.md`](file:///workspaces/Infrastructure/Teck.Cloud/.github/skills/csharp-xunit/SKILL.md) |
| .NET best practices | [`.github/skills/dotnet-best-practices/SKILL.md`](file:///workspaces/Infrastructure/Teck.Cloud/.github/skills/dotnet-best-practices/SKILL.md) |
| Design patterns | [`.github/skills/dotnet-design-pattern-review/SKILL.md`](file:///workspaces/Infrastructure/Teck.Cloud/.github/skills/dotnet-design-pattern-review/SKILL.md) |
| EF Core | [`.github/skills/ef-core/SKILL.md`](file:///workspaces/Infrastructure/Teck.Cloud/.github/skills/ef-core/SKILL.md) |
| NuGet packages | [`.github/skills/nuget-manager/SKILL.md`](file:///workspaces/Infrastructure/Teck.Cloud/.github/skills/nuget-manager/SKILL.md) |
| Git commit | [`.github/skills/git-commit/SKILL.md`](file:///workspaces/Infrastructure/Teck.Cloud/.github/skills/git-commit/SKILL.md) |
| Dockerfile | [`.github/skills/multi-stage-dockerfile/SKILL.md`](file:///workspaces/Infrastructure/Teck.Cloud/.github/skills/multi-stage-dockerfile/SKILL.md) |
| Microsoft docs lookup | [`.github/skills/microsoft-docs/SKILL.md`](file:///workspaces/Infrastructure/Teck.Cloud/.github/skills/microsoft-docs/SKILL.md) |

## Gotchas

- A service's `.Infrastructure` references **only its own** `.Migrations.{provider}` project for the active provider — switching DB provider means rebuilding with a different migration project referenced.
- `Web.Edge` and `Web.Aggregate.Gateway` are scaffolded but currently contain only `bin/`+`obj/` — no source yet.
- Local dev entry point is **always** `Teck.Cloud.AppHost`, not individual services.

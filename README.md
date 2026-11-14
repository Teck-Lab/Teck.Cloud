# Teck.Cloud

Nx + Bun workspace for .NET services, Next.js web apps and React Native mobile apps.
SharedKernel.Core, Persistence and Infrastructure are implemented with unit and
Testcontainers integration tests. The [local Aspire and KIND setup](docs/local-development.md)
starts development dependencies and has foundation, Sentinel failover and Kubernetes
smoke tests. No business services or frontend have been migrated yet. See
[shared runtime](docs/shared-runtime.md) for composition and [coverage](docs/coverage.md) for the CI gate.

| Directory    | Responsibility                                               |
| ------------ | ------------------------------------------------------------ |
| src/apps     | User-facing apps, such as web-customer, web-admin and mobile |
| src/services | Backend services grouped by domain, including gateways       |
| src/packages | Reusable TypeScript workspace packages                       |
| src/shared   | Focused shared .NET libraries                                |
| src/aspire   | AppHost composition and service defaults                     |
| tests        | Unit/integration/architecture and assembled E2E projects     |
| deployment   | Application base manifests and environment overlays          |
| tools        | Repository tooling added when needed                         |

See [architecture](docs/architecture.md), [service structure](docs/service-structure.md),
[shared foundation](docs/shared-foundation.md), [multi-tenancy](docs/multi-tenancy.md), [events and communication](docs/messaging.md), [testing](docs/testing.md), [.NET build quality](docs/dotnet-quality.md), and
[CI and releases](docs/ci.md).

## Toolchain and commands

Bun 1.4.1, Node 24.18.0, .NET SDK 10.0.401 and Nx 24.0.6 are pinned in repository
configuration. TypeScript 6.0.2 retains the compiler API needed by the current Nx
plugin; TypeScript 7 is not compatible with that plugin yet. .NET allows newer patches within the selected SDK feature band.

Install with bun install --frozen-lockfile. Run bun run check for workspace
format/lint checks and bun run projects to inspect Nx projects. After adding
projects, dotnet restore Teck.Cloud.slnx prepares .NET dependencies before Nx
builds. bun run build and bun run test run existing targets, not placeholder tests.

Bun workspaces cover `src/apps/*` and `src/packages/*`. Packages declare `workspace:*`
dependencies. C# projects use NuGet and ProjectReference. Register new .NET projects
in the solution. No Next.js/Expo app or mobile test runner is
created yet; add their matching Nx plugins with the first applications.

Security automation and the SLSA Build L3 artifact pipeline are described in
[supply-chain security](docs/supply-chain.md). The [CI roadmap](docs/ci-roadmap.md)
records GitHub activation steps and application-dependent work still to implement.

Agent guidance starts at [AGENTS.md](AGENTS.md) and is scoped by source layer and
test lane. See the [instruction map](docs/agent-instructions.md) when extending it.

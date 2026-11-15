# Teck.Cloud

Nx + Bun monorepo for .NET services, Next.js and React Native.

## Read only the relevant guidance

Read this file, then the AGENTS.md files on the path to the files you will change.
Parent instructions still apply. Follow linked documents for the responsibility
being changed; do not load every sibling instruction file or the entire docs tree.
When work crosses a boundary, read the instructions for that boundary too.
Before designing tests, read the relevant tests guidance below.

| Working area                                | Scoped instructions                                          |
| ------------------------------------------- | ------------------------------------------------------------ |
| Service slices, endpoints and domain models | [services](src/services/AGENTS.md)                           |
| Shared .NET contracts and adapters          | [shared](src/shared/AGENTS.md), then the project's AGENTS.md |
| Web/mobile applications                     | [apps](src/apps/AGENTS.md)                                   |
| Shared TypeScript packages                  | [packages](src/packages/AGENTS.md)                           |
| Local stack and AppHost                     | [Aspire](src/aspire/AGENTS.md)                               |
| Unit/integration/architecture/E2E           | [tests](tests/AGENTS.md), then the test lane's AGENTS.md     |
| Kubernetes manifests                        | [deployment](deployment/AGENTS.md)                           |
| GitHub workflows and dependency automation  | [GitHub](.github/AGENTS.md)                                  |
| Repository scripts and coverage tooling     | [tools](tools/AGENTS.md)                                     |
| Documentation and instruction maintenance   | [docs](docs/AGENTS.md)                                       |

## Repository-wide rules

- Keep the src/apps, services, packages, shared and aspire boundaries. For layout
  or project-graph changes, read [architecture](docs/architecture.md). Do not
  create empty business projects or copy old service inventories.
- Bun owns JS dependencies and bun.lock. Keep Nx plugins aligned; use repository
  scripts and native language toolchains. Add actual project/runtime/codegen
  dependency edges; affected selection must include transitive consumers.
- .NET packages belong in Directory.Packages.props. Warnings are errors in source
  and tests. For C# or build-policy changes, read [quality](docs/dotnet-quality.md);
  do not weaken analyzers or remove generated compilation inputs. Run
  bun run verify:dotnet-policy after changing packages, analyzers or C# policy.
- Implement behavior with focused RED/GREEN tests, then verify the assembled
  feature at its real boundary. Preserve the [coverage ratchet](docs/coverage.md);
  do not narrow tests or measurement to conceal regressions.
- Tenant-owned work requires validated scope; no default-tenant or global fallback.
  Never commit credentials, expose secrets in telemetry, or bypass ownership/auth.
- Before asserting existing behavior, trace inherited contracts, callers,
  registrations and tests. Use [the verification gate](docs/agent-instructions.md#verify-a-contract-before-claiming-it-works)
  for foundation changes.
- Run checks appropriate to the changed paths; report what actually passed and
  what remains unverified. Empty target sets and documentation are not evidence.
- Use Conventional Commits. Agent remote publication uses the configured GitHub
  App MCP and verified signatures, never personal Git/gh credentials.
- Runtime agent roles and orchestration skills belong in the host/plugin.
  Maintain these scoped rules using [the instruction map](docs/agent-instructions.md).

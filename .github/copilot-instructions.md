# Teck.Cloud Copilot Instructions

This repository uses the rule set under `.github/instructions/*.instructions.md` as the canonical AI coding guidance.

## Instruction Precedence

1. Repository source of truth and existing code patterns
2. `Directory.Build.props`, `Directory.Packages.props`, `global.json`, `nuget.config`, `stylecop.json`, `.editorconfig`
3. `.github/instructions/*.instructions.md`
4. Task-specific user instructions

## Core Expectations

- Follow clean architecture boundaries used in `src/services/*`.
- Keep API projects transport-thin: endpoint classes only.
- Keep request/validator types in Application feature slices.
- Prefer minimal, safe changes; do not refactor unrelated code.
- Keep tests aligned with project conventions in `tests/` and CI workflows.
- Use .NET 10 conventions in this repository unless a project explicitly requires otherwise.

## Current Repository Layout

- Shared building blocks: `src/buildingblocks/SharedKernel.*`
- Service implementations: `src/services/{basket,catalog,customer,order}/*`
- API gateways: `src/gateways/Web.Public.Gateway`, `src/gateways/Web.Admin.Gateway`
- Aspire host/defaults: `src/aspire/Teck.Cloud.AppHost`, `src/aspire/Teck.Cloud.ServiceDefaults`
- Auth assets: `src/auth/*`, `src/keycloak/*`
- Test layout: `tests/unit/*`, `tests/integration/*`, `tests/architecture/*`, `tests/e2e/*`

## Application Structure Conventions

- Use capability-first application folders: `<Capability>/Features/<UseCase>/V1`.
- Keep request/validator types inside the same versioned use-case folder.
- Keep response models under capability folders (for example `<Capability>/Responses`).
- Keep read models and repository abstractions under capability folders.
- Keep domain event handlers under `<Capability>/EventHandlers/DomainEvents`.
- Do not use root-level `Application/Features/*` folders.
- For gRPC handlers, folder path may be `Grpc/gRpc/V1` while C# namespaces remain `*.Grpc.V1`.

## Canonical Rule Set

- `.github/instructions/ai-rules-architecture-project-structure.instructions.md`

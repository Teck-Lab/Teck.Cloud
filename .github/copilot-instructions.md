# Teck.Cloud Copilot Instructions

This repository uses the rule set under `docs/ai/rules` as the canonical AI coding guidance.

## Instruction Precedence

1. Repository source of truth and existing code patterns
2. `Directory.Build.props`, `Directory.Packages.props`, `global.json`, `nuget.config`, `stylecop.json`, `.editorconfig`
3. `docs/ai/rules/README.md` and linked rule files (mirrored to `.github/instructions/*.instructions.md`)
4. Task-specific user instructions

## Core Expectations

- Follow clean architecture boundaries used in `src/services/*`.
- Prefer minimal, safe changes; do not refactor unrelated code.
- Keep tests aligned with project conventions in `tests/` and CI workflows.
- Use .NET 10 conventions in this repository unless a project explicitly requires otherwise.

## Current Repository Layout

- Shared building blocks: `src/buildingblocks/SharedKernel.*`
- Service implementations: `src/services/catalog/*`, `src/services/customer/*`
- API gateways: `src/gateways/Web.Edge`, `src/gateways/Web.Aggregate.Gateway`
- Aspire host/defaults: `src/aspire/Teck.Cloud.AppHost`, `src/aspire/Teck.Cloud.ServiceDefaults`
- Auth assets: `src/auth/*`, `src/keycloak/*`
- Test layout: `tests/unit/*`, `tests/integration/*`, `tests/architecture/*`, `tests/e2e/*`

## Application Structure Conventions

- Prefer feature-first application folders: `<Aggregate>/Features/<UseCase>/V1`.
- Keep response models under `<Aggregate>/Responses`.
- Keep domain event handlers under `<Aggregate>/EventHandlers/DomainEvents`.
- For gRPC handlers, folder path may be `Grpc/gRpc/V1` while C# namespaces remain `*.Grpc.V1`.

## Canonical Rule Set

- `docs/ai/rules/README.md`

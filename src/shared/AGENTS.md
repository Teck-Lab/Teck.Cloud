# Shared .NET foundations

Read [shared foundation](../../docs/shared-foundation.md) when changing shared
contracts and [shared runtime](../../docs/shared-runtime.md) when changing adapter
composition. Read the relevant child AGENTS.md before editing its project.

- [Core](SharedKernel.Core/AGENTS.md): generic domain, CQRS, repository, cache and
  operation/tenant contracts.
- [Persistence](SharedKernel.Persistence/AGENTS.md): EF, Finbuckle persistence,
  transactions, FusionCache and Redis.
- [Infrastructure](SharedKernel.Infrastructure/AGENTS.md): ingress/auth, Wolverine,
  hosting, health, telemetry and API composition.
- Keep service entities, mappings, migrations and business policy service-owned.
  Pricing, Licensing and Devices are not generic foundations. This split does not
  impose a fourth Persistence project on each service.
- Core cannot depend on Persistence/Infrastructure; Persistence cannot depend on
  Infrastructure. Preserve these boundaries with the shared architecture suite.
- Support global, tenant and mixed consumers explicitly. For scope changes read
  [multi-tenancy](../../docs/multi-tenancy.md); missing scope is never permission
  to access global or another tenant's data.
- Validate affected consumers and the appropriate unit/integration tests. Ported
  source alone proves neither provider support, atomic delivery nor Native AOT.

# Architecture tests

Read [architecture](../../docs/architecture.md) and, for service boundaries,
[service structure](../../docs/service-structure.md).

- Use the existing ArchUnitNET/xUnit setup against actual compiled projects.
- Preserve Core -> no Persistence/Infrastructure and Persistence -> no
  Infrastructure. Add service-specific reference, slice and handler rules with
  actual service projects, not empty scaffolding.
- Enforce query/write dependency separation and service contract ownership without
  banning legitimate Host-to-Domain composition documented in the service baseline.
- Reconcile legacy rules with the selected structure; do not copy contradictory
  tests and then weaken them to pass.
- Prove a rule detects its prohibited dependency with focused fixtures when adding
  a new enforcement mechanism. Tags and prose alone do not enforce architecture.
- Runtime authorization, SQL permissions, tenant isolation and outbox atomicity
  still require integration evidence.

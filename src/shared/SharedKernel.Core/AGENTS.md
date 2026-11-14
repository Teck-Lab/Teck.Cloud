# SharedKernel.Core: contracts and domain primitives

Read [shared foundation](../../../docs/shared-foundation.md) for retained contracts.
For events read [messaging](../../../docs/messaging.md); for tenant contracts read
[multi-tenancy](../../../docs/multi-tenancy.md).

- Keep domain primitives independent of EF, ASP.NET, Finbuckle and Wolverine runtime
  adapters. Do not put service workflows, pricing or licensing policy here.
- Aggregate entry points explicitly implement IAggregateRoot. BaseEntity alone
  is not a root; child mutations belong behind aggregate methods.
- IDomainEvent and IIntegrationEvent are distinct contracts; no concrete type
  implements both. Domain events stay local and entities do not publish to a broker.
- IUnitOfWork exposes the application's SaveChangesAsync boundary, not transaction
  or context lifetime ownership. Read contracts expose no tracking/filter bypass.
- Retain Ardalis specification interfaces rather than inventing a new DSL.
  Read [specifications](../../../docs/specifications.md) before repository API changes.
- Keep tenant/global scope explicit and framework-neutral. Do not add TenantId to
  every entity or infer trusted scope from arbitrary input.
- Options/IOptionsRoot is a marker; bind and validate settings in Host composition.
- Test domain rules/contracts in tests/unit and dependency direction in
  tests/architecture. Framework adapters and real storage proofs belong elsewhere.

# .NET business services and gateways

Before creating or migrating a service, read [service structure](../../docs/service-structure.md).
When adding its real Domain/Application/Host projects, add focused AGENTS.md files
using [the layer checklist](../../docs/agent-instructions.md#new-service-layers).
Do not create empty projects just to host instructions.

- Normally use Domain, Application and Host. Gateways/stateless workers need only
  actual responsibilities. SharedKernel.Persistence is reusable infrastructure,
  not a mandatory fourth service project.
- Keep capability-based vertical slices under
  Application/<Capability>/Features/<UseCase>/V1. No references to another service's
  implementation or from Application to Host.
- Domain owns aggregate invariants. Application handlers use read/write repositories,
  IUnitOfWork and ErrorOr. Wolverine owns CQRS and transactional inbox/outbox;
  query handlers must not receive write access.
- Host uses standard FastEndpoints, generated discovery, ServiceScan with explicit
  lifetimes, source-generated JSON and Scalar. Keep HTTP adapters thin; no custom
  endpoint hierarchy or second mediator.
- For events/transports read [messaging](../../docs/messaging.md): local domain
  events, explicit RabbitMQ integration routes, gRPC synchronous service calls.
- For tenant-owned features read [multi-tenancy](../../docs/multi-tenancy.md).
  Both read/write paths enforce validated scope; migrations remain service-owned.
- For host composition read [shared runtime](../../docs/shared-runtime.md),
  [health](../../docs/health.md) and [observability](../../docs/observability.md).
- Prefer supported generation/static loading; retain EF/JIT today without claiming
  full Native AOT. Add unit, real Testcontainers and architecture tests with the
  first slice; actual assembled journeys use Aspire E2E.
- Each csproj participates in Nx; the Host image is the deployable/release unit.
  Reconcile legacy inconsistencies before copying code or architecture tests.

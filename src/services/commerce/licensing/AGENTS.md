# Commerce Licensing service

Issues and verifies signed database-entitlement licenses only. Orders owns payment,
approval, readiness and effective-eligibility facts; Licensing never provisions a
database, never reads a placement credential and never calls Licensing-owned data
from an edge route.

- [Domain](Licensing.Domain/AGENTS.md) owns grant and generation invariants.
- [Application](Licensing.Application/AGENTS.md) owns generation fencing, issuance
  and verification use cases and the internal Orders port.
- [Infrastructure](Licensing.Infrastructure/AGENTS.md) owns EF contexts, migrations,
  `Standard.Licensing` and the scoped Orders gRPC adapter.
- [Host](Licensing.Host/AGENTS.md) is the separate gRPC process, auth/tenant
  composition and settings contract.

Read [service structure](../../../../docs/service-structure.md),
[messaging](../../../../docs/messaging.md), [multi-tenancy](../../../../docs/multi-tenancy.md)
and the commerce [v1 contract](../../../../docs/contracts/commerce/v1/README.md).
The only integration endpoints are the internal `GetCurrentLicense`/`VerifyLicense`
gRPC calls; there is no browser surface. Publishes `commerce.license-state.v1` and
consumes `commerce.license-desired.v1` through Wolverine/RabbitMQ with the durable
PostgreSQL inbox/outbox. No other service implementation is referenced.

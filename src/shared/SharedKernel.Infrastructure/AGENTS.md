# SharedKernel.Infrastructure: transport and host composition

Read only the contracts for the area being changed:

| Area                              | Canonical guidance                                |
| --------------------------------- | ------------------------------------------------- |
| Wiring, auth, FastEndpoints, gRPC | [shared runtime](../../../docs/shared-runtime.md) |
| CQRS, events, inbox/outbox        | [messaging](../../../docs/messaging.md)           |
| Identity and operation scope      | [multi-tenancy](../../../docs/multi-tenancy.md)   |
| Serilog, OTEL, masking, profiling | [observability](../../../docs/observability.md)   |
| Probes and required dependencies  | [health](../../../docs/health.md)                 |
| Gateway output caching            | [caching](../../../docs/caching.md)               |

- Use standard FastEndpoints classes and generated discovery, ErrorOr handler
  results, generated JSON metadata and Host-only HTTP result mapping. No custom
  endpoint base hierarchy or competing mediator/bus.
- Async cross-service traffic uses Wolverine/RabbitMQ; sync uses gRPC. HTTP is
  external ingress. Keycloak OIDC/UMA/Admin APIs use their native HTTP protocols.
  Domain events stay local; integration contracts route explicitly.
  Preserve native durable inbox/outbox and control-queue configuration.
- Keycloak integration uses Keycloak.AuthServices native authentication, organization
  requirements, UMA and SDK clients. Read [identity tenancy](../../../docs/identity-tenancy.md)
  and [composition](../../../docs/keycloak-authservices.md). One organization UUID
  flows through Finbuckle, Wolverine and operation scope; no parallel tenant headers.
- Validate tenant authority at ingress and consumers; headers/payload fields alone
  grant nothing. Propagate the validated scope into Wolverine, gRPC and persistence.
- Preserve correlated trace/span logs across transport boundaries. Use shared
  OTLP/Serilog composition and application masking; never log secrets or use
  tenant/request/trace IDs as metric labels. Alloy is additional redaction.
- Keep required readiness dependencies enabled, database checks provider-dependent,
  liveness independent of external outages and diagnostics internal/sanitized.
- Runtime Redis uses Sentinel. Output caching is explicit and policy-scoped; do
  not automatically cache authenticated, tenant or administrative responses.
- Unit-test decisions; prove real transport, persistence and HTTP behavior using
  Testcontainers fixtures. End-to-end traces/login flows need actual Aspire services.

For signed assurance and freshness on administrative operations, read
[identity MFA](../../../docs/identity-mfa.md). Do not substitute token issuance time
for original authentication time or combine claims from separate identities.

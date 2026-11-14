# Service integration tests

Read [test tooling](../../docs/test-tooling.md). For storage/messages read the
relevant [shared runtime](../../docs/shared-runtime.md) and
[messaging](../../docs/messaging.md) contracts.

- Use Testcontainers for real dependencies; never boot Aspire in this lane.
  Use FastEndpoints.Testing/WebApplicationFactory for the actual HTTP pipeline.
- Verify committed database effects through fresh contexts/connections. Do not
  substitute EF InMemory or mocked repositories for relational/transaction proof.
- Exercise real Wolverine/RabbitMQ for delivery, rollback, retry and inbox/outbox
  claims. Use a real HTTP/2 server when gRPC transport/TLS is the behavior under test.
- Prove read-only credentials, migrations and cross-tenant isolation where relevant.
  Include duplicate/replay and interrupted-delivery cases for durable workflows.
- Start dependencies before the host; bound readiness and dispose only owned
  resources. Shared fixtures need isolated data/tenant/correlation identities.
- Standalone Redis is allowed for focused adapter tests; Sentinel topology/failover
  is verified in Aspire E2E. State any substituted boundary explicitly.
- Keep fixture-only host code under tests/fixtures; do not introduce test shortcuts
  into production registration or disable real guards to make tests pass.

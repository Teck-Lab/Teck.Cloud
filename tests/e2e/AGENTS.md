# Assembled and deployed E2E

Read [testing](../../docs/testing.md) and [local development](../../docs/local-development.md),
then the web or kubernetes child instructions.

- Aspire owns one bounded test lifecycle. Use actual participating services and
  unique resources; cancel/clean up only what this run owns.
- Preserve authentication, tenant and transport boundaries. Do not replace real
  gateway/service/broker hops with local dispatch to obtain an E2E pass.
- Record actual observable outcomes and failure diagnostics without secrets.
  Process readiness is a prerequisite, not feature acceptance.
- Current suites prove foundation dependencies, Sentinel recovery and Kubernetes
  smoke behavior. No business application/browser journey exists yet.

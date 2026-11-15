# Aspire composition

Read [local development](../../docs/local-development.md) before profile/lifecycle
changes, [KIND platform](../../docs/kind-platform.md) for operator composition,
and [Redis Sentinel](../../docs/redis-sentinel.md) before Redis networking.

- AppHost owns the development, sentinel and kind profiles. Add real projects with
  WithReference/WaitFor and explicit Nx runtime dependencies.
- Use pinned compatible hosting integrations and the selected Docker/Podman engine;
  do not hard-code a developer machine IP or silently switch engines.
- Redis uses Sentinel, including local development. Its advertised address must be
  reachable from both containers and host clients.
- Keep generated secrets out of source. Preserve Keycloak's realm-specific authority,
  PKCE and restricted redirects; disabling authentication is not a local fix.
- The local KIND development profile reuses one cluster per checkout. E2E/CI
  must use unique session-owned clusters and kubeconfigs with bounded cleanup.
  Never target an existing production cluster or another developer's cluster.
- Apply actual deployment manifests and reachable candidate images for application
  KIND tests. The smoke Job proves infrastructure only.
- Aspire owns assembled E2E lifecycle, not service integration tests. Those remain
  Testcontainers. Read the relevant tests/e2e instructions for validation.
- Service host telemetry/health composition belongs in shared Infrastructure, not
  duplicated AppHost-specific implementations.

- KIND uses TECK_KIND_MODE: full by default, sandbox only when explicitly selected
  (the Orca VM supplies it). Keep reduced mode free of Cilium/Istio ambient and
  never silently downgrade a full-mode failure. Verify both modes after changes.

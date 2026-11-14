# Kubernetes deployment E2E

Read [local development](../../../docs/local-development.md) and
[deployment guidance](../../../deployment/README.md).

- Aspire/KIND creates a unique disposable cluster with its own kubeconfig. Never
  apply to the ambient/default kubeconfig or delete another run's cluster.
- Use the pinned node image and compatible KIND/kubectl with the selected engine.
  Report kernel/network limitations instead of weakening the feature under test.
- Application tests deploy actual deployment overlays and the same candidate image
  version. Load images or use a reachable registry before applying manifests.
- Wait for bounded rollout readiness, then assert real gateway/application behavior
  and relevant operator effects. An alternate generated chart does not verify
  repository manifests.
- The basic smoke Job proves scheduling/DNS/lifecycle only. The platform fixtures
  separately prove HTTPS/Rollouts, CNPG persistence, authenticated Sentinel discovery
  and OpenBao initialization. Do not replace those assertions with controller readiness.
- Dispose the owned cluster on success/failure and verify cleanup. Preserve useful,
  sanitized logs when startup/deployment fails.

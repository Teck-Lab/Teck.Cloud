# Application deployment manifests

Read [deployment ownership](README.md) and [local KIND workflow](../docs/local-development.md).

- Keep reusable application resources in base and overlays by environment:
  local, development, staging and production. Do not add per-cluster application folders.
- GitOps references the versioned application overlay and supplies cluster-specific
  configuration. Terraform/server provisioning and shared platform installation
  belong outside this repository.
- Tie images, migrations and manifests to the same candidate/release. Validate
  actual overlays in KIND rather than testing different generated manifests.
- Preserve required auth/tenant behavior and [health probe semantics](../docs/health.md).
  Keep dependency diagnostics internal; do not use external outages as liveness.
- Use secret references, least privilege and explicit resource settings. Do not
  commit credentials or silently relax security in local overrides.
- Add real manifests with actual applications; no placeholder deployment claims.
  Include manifest/build inputs in Nx affected selection and validate rendering,
  configuration scanning and the relevant deployed E2E.

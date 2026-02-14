# Deployment with ArgoCD

## Intent

Manage Kubernetes deployments from a repository-root `deployment/` folder using ArgoCD GitOps.

## Rules

- Keep deployment manifests in `deployment/` at repository root.
- ArgoCD application definitions must reference only services/projects that exist in this repo.
- Prefer an app-of-apps layout for consistent onboarding and promotion.
- Keep namespace, sync policy, and destination explicit in ArgoCD `Application` manifests.
- Keep runtime configuration externalized via env/config maps/secrets, not hardcoded credentials.
- Use immutable image tags from CI release pipelines whenever possible.

## Repository Scope

Current deployable service set in this repository:

- `catalog-api`
- `customer-api`
- `web-bff`

Add new deployment apps only when corresponding source projects exist under `src/services` or `src/gateways`.

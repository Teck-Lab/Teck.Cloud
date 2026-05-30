---
description: "Use when working across multiple repos or implementing a change that spans Teck.Cloud, Teck.GitOps, Teck.Terraform, or Teck.Web. Covers how the repos relate and how the end-to-end deployment pipeline works."
---
# Cross-Repo Platform Overview

## Repository Relationships

```
Teck.Cloud       — .NET 10 microservices (source of truth for business logic)
Teck.GitOps      — GitOps delivery (ArgoCD + Kargo, source of truth for cluster state)
Teck.Terraform   — Infrastructure provisioning (OpenTofu, source of truth for platform tooling)
Teck.Web         — Frontend applications (Next.js 15 App Router, TurboRepo)
```

These repos are **intentionally decoupled** — Teck.Cloud knows nothing about Kubernetes; Teck.GitOps knows nothing about C# code. The only coupling is through:
- Container image tags (Teck.Cloud produces → Teck.GitOps consumes)
- Service DNS names (defined by Kubernetes Service resources in Teck.GitOps overlays)
- API contracts (OpenAPI specs in Teck.Cloud, consumed by Teck.Web codegen)

## End-to-End Deployment Pipeline

1. **Code change** lands in `Teck.Cloud` → GitHub Actions CI runs tests, builds Docker image, pushes to registry with SHA tag.
2. **Kargo Warehouse** in `Teck.GitOps` watches the container registry for new tags matching each service's image policy.
3. **Kargo promotes Freight** through stages: `development` → (approval gate) → `production`. Each promotion updates the image tag in the relevant Kustomize overlay.
4. **ArgoCD ApplicationSet** detects the overlay change (polling or webhook) and syncs the Kubernetes resources to the cluster.
5. **Cluster** (K3s/K3k, provisioned by `Teck.Terraform`) runs the updated workloads.

## Stage Environments

| Stage | Path | Cluster Context |
|-------|------|-----------------|
| development | `Teck.GitOps/apps/development/` | in-cluster ArgoCD |
| production | `Teck.GitOps/apps/production/` | in-cluster ArgoCD |

## Adding a New Service End-to-End

When adding a new service (e.g., `widget-api`) you must touch **all four repos**:

1. **Teck.Cloud**: Create `src/services/widget/{Widget.Domain,Widget.Application,Widget.Infrastructure,Widget.Api}`. Add Aspire registration in `AppHost`.
2. **Teck.GitOps**: Add `apps/{development,production}/teck-cloud/{teck-cloud-core}/widget-api/` overlay directory with `kustomization.yaml` + `config.yaml`. Register in the parent `kustomization.yaml`.
3. **Teck.Terraform**: If a new database or broker topic is needed, add a module or resource. Update any Kargo `Warehouse` image subscriptions if the image repo differs.
4. **Teck.Web**: Add a generated API client pointed at the new service's OpenAPI spec. Wire into `turbo.json` if a separate app or package is warranted.

## Bootstrap Order

Cluster bootstrap happens in this order (managed by `Teck.GitOps/bootstrap/`):

1. `cluster-resources` — namespaces, RBAC, CRDs (applied with raw `kubectl`)
2. `essentials` — cert-manager, external-secrets-operator, gateway-api, Istio (ArgoCD wave 0–1)
3. `apps` — platform tooling + Teck.Cloud services (ArgoCD wave 2+)

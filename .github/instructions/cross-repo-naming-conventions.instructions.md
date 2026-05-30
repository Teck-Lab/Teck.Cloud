---
description: "Use when naming services, images, Kubernetes resources, Helm values keys, TypeScript packages, or Terraform modules. Covers naming standards consistent across Teck.Cloud, Teck.GitOps, Teck.Terraform, and Teck.Web."
---
# Cross-Repo Naming Conventions

## Canonical Service Names

These names are the single source of truth used in **all** repos:

| Service | .NET Project Prefix | Container Image | K8s Name | Kargo Stage Name |
|---------|--------------------|-----------------|-----------|--------------------|
| Basket API | `Basket.*` | `basket-api` | `basket-api` | `basket-api` |
| Catalog API | `Catalog.*` | `catalog-api` | `catalog-api` | `catalog-api` |
| Customer API | `Customer.*` | `customer-api` | `customer-api` | `customer-api` |
| Order API | `Order.*` | `order-api` | `order-api` | `order-api` |
| Public Gateway (YARP) | `Web.Public.Gateway` | `yarp-gateway` | `yarp-gateway` | `yarp-gateway` |
| Admin Gateway | `Web.Admin.Gateway` | `admin-gateway` | `admin-gateway` | `admin-gateway` |
| Image Generator | `Image.Generator.*` | `image-generator` | `image-generator` | `image-generator` |

**Rule**: Service names are always `kebab-case`. The .NET project prefix is `PascalCase`. Never use underscores in K8s names or image names.

## Namespace Conventions

| Scope | Namespace |
|-------|-----------|
| Teck.Cloud microservices | `teck-cloud-core` |
| Auth (Keycloak, Dex) | `auth` |
| Monitoring | `monitoring` |
| Networking (Istio, gateways) | `network` |
| Platform tooling (ArgoCD, Kargo) | `argocd`, `kargo` |
| Databases (CNPG) | `cnpg-system` |
| Secrets (ESO, OpenBao) | `external-secrets`, `openbao` |

## Image Naming

- Container images follow `{service-name}:{tag}` (e.g., `basket-api:sha-abc123`).
- Tags from CI are always SHA-based (`sha-{7-char-hash}`). Never use `latest` in GitOps overlays.
- Kargo Warehouse image policies subscribe to `^sha-[a-f0-9]{7}$` pattern.

- Container images follow `{service-name}:{tag}` (e.g., `basket-api:sha-abc123`).
- Tags from CI are always SHA-based (`sha-{7-char-hash}`). Never use `latest` in GitOps overlays.
- Kargo Warehouse image policies subscribe to `^sha-[a-f0-9]{7}$` pattern.

## Terraform Module Names

- Terraform modules in `Teck.Terraform/modules/` use `kebab-case` directory names matching the tool they provision (e.g., `cert-manager`, `argocd`, `cnpg`).
- Module output names use `snake_case` (Terraform convention).

## TypeScript Package Names

- Internal TurboRepo packages use the `@teck/` scope (e.g., `@teck/ui`, `@teck/tailwind-config`).
- App packages use `@teck/{app-name}` (e.g., `@teck/web`, `@teck/web-dashboard`).

## Kustomize Overlay Paths

```
apps/{env}/teck-cloud/teck-cloud-core/{service-name}/
apps/{env}/platform/{tool-name}/
cluster-resources/{env}/
essentials/{category}/{tool-name}/
```

Where `{env}` is `development`, `production`, or `in-cluster`.

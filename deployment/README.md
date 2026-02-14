# Deployment

This folder contains GitOps manifests for ArgoCD.

## Structure

- `argocd/` - ArgoCD `AppProject` + root/child `Application` manifests
- `manifests/` - Kubernetes manifests per service in this repository

## Services in Scope

- `catalog-api`
- `customer-api`
- `web-bff`

## Usage

1. Apply the ArgoCD project and root application:
   - `deployment/argocd/project.yaml`
   - `deployment/argocd/root-application.yaml`
2. ArgoCD will reconcile child apps from `deployment/argocd/apps/`.
3. Update image tags in each service deployment manifest as part of release promotion.

# Deployment Layout

This directory holds Kubernetes deployment overlays for Teck.Cloud applications.

Preview overlays currently follow this layout:

```text
deployment/
  <app>/
    overlays/
      preview/
        deployment.yaml
        service.yaml
        kustomization.yaml
        httproute.yaml
```

The GitOps preview generator expects:

1. A preview overlay at `deployment/<app>/overlays/preview`.
2. An `HTTPRoute` named exactly the same as `<app>`.
3. A placeholder hostname annotation and hostname entry that Argo CD can patch per PR.

`<app>` is the shared app key. It must line up with the GitOps inventory entry at `apps/in-cluster/preview/<app>/config.yaml` in `Teck.GitOps`.

Current app keys:

1. `catalog-api`
2. `customer-api`
3. `aggregate-gateway`
4. `yarp-gateway`

Each preview overlay now contains a minimal `Deployment`, `Service`, and `HTTPRoute` scaffold.

These are only starter manifests. Image names, environment variables, probes, resources, and any app-specific objects still need to be aligned with the actual application runtime.
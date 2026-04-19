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

Database-backed services also keep generated Flyway SQL assets under the same app folder:

```text
deployment/
  <app>/
    database/
      flyway/
      kustomization.yaml
      job.postgres.yaml
        postgres/
        sqlserver/
        mysql/
```

These database assets are intended to be pipeline-managed. The Flyway workflow regenerates the SQL for same-repository pull requests and commits the updated files back to the branch so GitOps preview generation can consume the checked-in artifacts.

    The `database/flyway` folder is the app-owned base for migration execution. It defines the provider-specific Flyway `Job` and the `ConfigMap` generator that packages the checked-in SQL files for mounting into the official Flyway container. Environment-specific secret names, namespaces, and orchestration can still be patched from the central GitOps repository.

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

Only services with owned relational schemas should get a `database/` folder. In the current repository that means `catalog-api` and `customer-api`.

Each provider gets its own Flyway directory because the generated SQL is provider-specific and cannot be shared across PostgreSQL, SQL Server, and MySQL. The current CI flow generates PostgreSQL first while preserving the directory layout for SQL Server and MySQL.

Each preview overlay now contains a minimal `Deployment`, `Service`, and `HTTPRoute` scaffold.

These are only starter manifests. Image names, environment variables, probes, resources, and any app-specific objects still need to be aligned with the actual application runtime.
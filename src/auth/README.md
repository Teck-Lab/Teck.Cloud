# Teck Auth (Keycloak)

Custom Keycloak image for Teck.Cloud, based on [Phase Two Keycloak](https://github.com/p2-inc/keycloak) with the [Tailcloakify](https://github.com/ALMiG-Kompressoren-GmbH/tailcloakify) theme.

## Image

- **Base:** `quay.io/phasetwo/phasetwo-keycloak:26.5.2`
- **Theme:** Tailcloakify (JAR added to `/opt/keycloak/providers/`)
- **Tags:** Same semantic version as the rest of the repo (e.g. `v1.2.3`), published to GHCR as `ghcr.io/<org>/<repo>/auth:<version>`.

## Build args

| Arg | Default | Description |
|-----|---------|-------------|
| `VERSION` | 1.0.0 | Image version (set by CI from release tag) |
| `BUILD_DATE` | - | OCI image created timestamp |
| `VCS_REF` | - | Git commit SHA |
| `VCS_URL` | - | Repository URL |
| `KEYCLOAK_VERSION` | 26.5.2 | Base Keycloak version |

## Local build

```bash
docker build -t teck-auth:local --build-arg VERSION=0.0.0-dev src/auth
```

## CI/CD

The auth image is built and published by `.github/workflows/docker-publish.yaml` when `src/auth/Dockerfile` exists. It uses the same version as other services (unified versioning from Auto).

## Theme configuration

Tailcloakify and Phase Two are configured via Keycloak environment variables. See Phase Two and Tailcloakify documentation for theme and realm options.

# Teck Auth (Keycloak)

Custom Keycloak image for Teck.Cloud, based on the [official Keycloak image](https://www.keycloak.org/server/containers) with the [keycloak-shadcn](https://github.com/emirhannaneli/keycloak-shadcn) theme extension.

## Image

- **Base:** `quay.io/keycloak/keycloak:26.5.23`
- **Theme:** keycloak-shadcn (JAR added to `/opt/keycloak/providers/`)
- **Tags:** Same semantic version as the rest of the repo (e.g. `v1.2.3`), published to GHCR as `ghcr.io/<org>/<repo>/auth:<version>`.

## Build args

| Arg | Default | Description |
|-----|---------|-------------|
| `VERSION` | 1.0.0 | Image version (set by CI from release tag) |
| `BUILD_DATE` | - | OCI image created timestamp |
| `VCS_REF` | - | Git commit SHA |
| `VCS_URL` | - | Repository URL |
| `KEYCLOAK_VERSION` | 26.5.23 | Base Keycloak version |

## Local build

```bash
docker build -t teck-auth:local --build-arg VERSION=0.0.0-dev src/auth
```

## CI/CD

The auth image is built and published by `.github/workflows/docker-publish.yaml` when `src/auth/Dockerfile` exists. It uses the same version as other services (unified versioning from Auto).

## Theme configuration

The image includes the keycloak-shadcn extension JAR. Configure theme behavior via Keycloak realm/theme settings and relevant environment variables.

# Dockerfile for Teck Keycloak Auth
# Based on official Keycloak distribution
ARG KEYCLOAK_VERSION=26.5.3
ARG VERSION=1.0.0

FROM docker.io/keycloak/keycloak:${KEYCLOAK_VERSION} as builder

USER 1000
WORKDIR /opt/keycloak

ADD --chown=1000:0 https://github.com/emirhannaneli/keycloak-shadcn/releases/download/v1.0.25/keycloak-shadcn-26.2-and-above.jar /opt/keycloak/providers/keycloak-shadcn-theme.jar

RUN /opt/keycloak/bin/kc.sh build

FROM docker.io/keycloak/keycloak:${KEYCLOAK_VERSION}
ARG VERSION
ARG KEYCLOAK_VERSION

USER 1000
WORKDIR /opt/keycloak

COPY --from=builder /opt/keycloak/ /opt/keycloak/

LABEL org.opencontainers.image.title="Teck Keycloak Auth" \
      org.opencontainers.image.description="Keycloak authentication service" \
      org.opencontainers.image.version="${VERSION}" \
      org.opencontainers.image.authors="Teck Team" \
      org.opencontainers.image.vendor="Teck" \
      org.opencontainers.image.licenses="MIT" \
      org.opencontainers.image.base.name="docker.io/keycloak/keycloak:${KEYCLOAK_VERSION}"

LABEL com.teck.service.name="auth" \
      com.teck.service.type="keycloak" \
      com.teck.keycloak.version="${KEYCLOAK_VERSION}" \
      com.teck.keycloak.distribution="official"

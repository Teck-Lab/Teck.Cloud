# Dockerfile for Teck Keycloak Auth
# Based on Phase Two Keycloak distribution
ARG KEYCLOAK_VERSION=26.5.2

FROM quay.io/phasetwo/phasetwo-keycloak:${KEYCLOAK_VERSION}
ARG VERSION=1.0.0
ARG KEYCLOAK_VERSION

USER 1000
WORKDIR /opt/keycloak

LABEL org.opencontainers.image.title="Teck Keycloak Auth" \
      org.opencontainers.image.description="Keycloak authentication service with Phase Two extensions" \
      org.opencontainers.image.version="${VERSION}" \
      org.opencontainers.image.authors="Teck Team" \
      org.opencontainers.image.vendor="Teck" \
      org.opencontainers.image.licenses="MIT" \
      org.opencontainers.image.base.name="quay.io/phasetwo/phasetwo-keycloak:${KEYCLOAK_VERSION}"

LABEL com.teck.service.name="auth" \
      com.teck.service.type="keycloak" \
      com.teck.keycloak.version="${KEYCLOAK_VERSION}" \
      com.teck.keycloak.distribution="phasetwo"

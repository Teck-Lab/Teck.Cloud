# Multi-stage Dockerfile for custom Keycloak with Tailcloakify theme
ARG KEYCLOAK_VERSION=26.5.2
ARG TAILCLOAKIFY_VERSION=1.2.2
ARG TAILCLOAKIFY_JAR_URL=https://github.com/ALMiG-Kompressoren-GmbH/tailcloakify/releases/download/v${TAILCLOAKIFY_VERSION}/keycloak-theme-for-kc-22-to-25.jar

FROM quay.io/phasetwo/phasetwo-keycloak:${KEYCLOAK_VERSION} AS builder
ARG TAILCLOAKIFY_JAR_URL
ADD --chown=keycloak:keycloak ${TAILCLOAKIFY_JAR_URL} /opt/keycloak/providers/tailcloakify-theme.jar

FROM quay.io/phasetwo/phasetwo-keycloak:${KEYCLOAK_VERSION}
ARG VERSION=1.0.0
ARG BUILD_DATE
ARG VCS_REF
ARG VCS_URL
ARG KEYCLOAK_VERSION
LABEL org.opencontainers.image.title="Teck Keycloak Auth" \
      org.opencontainers.image.description="Custom Keycloak authentication service with Phase Two and Tailcloakify theme" \
      org.opencontainers.image.version="${VERSION}" \
      org.opencontainers.image.created="${BUILD_DATE}" \
      org.opencontainers.image.authors="Teck Team" \
      org.opencontainers.image.source="${VCS_URL}" \
      org.opencontainers.image.revision="${VCS_REF}" \
      org.opencontainers.image.vendor="Teck" \
      org.opencontainers.image.licenses="MIT" \
      org.opencontainers.image.base.name="quay.io/phasetwo/phasetwo-keycloak:${KEYCLOAK_VERSION}"
LABEL com.teck.service.name="auth" \
      com.teck.service.type="keycloak" \
      com.teck.keycloak.version="${KEYCLOAK_VERSION}" \
      com.teck.keycloak.distribution="phasetwo"

# Copy entire keycloak directory to preserve Phase Two providers and filesystem state
COPY --from=builder /opt/keycloak /opt/keycloak
WORKDIR /opt/keycloak

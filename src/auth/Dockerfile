# Single-stage Dockerfile for custom Keycloak with Tailcloakify theme
ARG KEYCLOAK_VERSION=26.5.2
ARG TAILCLOAKIFY_VERSION=1.2.2
ARG TAILCLOAKIFY_JAR_URL=https://github.com/ALMiG-Kompressoren-GmbH/tailcloakify/releases/download/v${TAILCLOAKIFY_VERSION}/keycloak-theme-for-kc-22-to-25.jar

FROM quay.io/phasetwo/phasetwo-keycloak:${KEYCLOAK_VERSION}
ARG VERSION=1.0.0
ARG BUILD_DATE
ARG VCS_REF
ARG VCS_URL
ARG KEYCLOAK_VERSION
ARG TAILCLOAKIFY_JAR_URL

# Switch to root to add theme JAR
USER root

# Add Tailcloakify theme JAR with proper ownership (keycloak:root/0)
ADD --chown=1000:0 ${TAILCLOAKIFY_JAR_URL} /opt/keycloak/providers/tailcloakify-theme.jar
RUN chmod 0644 /opt/keycloak/providers/tailcloakify-theme.jar

# Switch back to keycloak user
USER 1000

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

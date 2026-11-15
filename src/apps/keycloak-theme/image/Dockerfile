# syntax=docker/dockerfile:1
FROM oven/bun:1.4.1@sha256:9e123d5fc069e29d519fd4c981afb61b8542ac80274771961136db1e4538d53e AS bun
FROM node:24.18.0-bookworm-slim@sha256:6f7b03f7c2c8e2e784dcf9295400527b9b1270fd37b7e9a7285cf83b6951452d AS node
FROM maven:3.9.16-eclipse-temurin-21@sha256:99e61abcff91a9b1333463bd8451fb18495d6eba9250ac66a338b518f8278320 AS theme
COPY --from=bun /usr/local/bin/bun /usr/local/bin/bun
COPY --from=node /usr/local/bin/node /usr/local/bin/node
COPY --from=node /usr/local/lib/node_modules/npm /usr/local/lib/node_modules/npm
RUN ln -s /usr/local/lib/node_modules/npm/bin/npm-cli.js /usr/local/bin/npm && ln -s /usr/local/lib/node_modules/npm/bin/npx-cli.js /usr/local/bin/npx
WORKDIR /source
COPY package.json bun.lock ./
COPY patches ./patches
COPY tsconfig.base.json ./
COPY src/apps/keycloak-theme ./src/apps/keycloak-theme
COPY src/packages/design-tokens ./src/packages/design-tokens
RUN bun install --frozen-lockfile --ignore-scripts
WORKDIR /source/src/apps/keycloak-theme
RUN bun run typecheck && bun run build && bun run build:theme
RUN test -s dist_keycloak/teck-keycloak-theme.jar

FROM quay.io/keycloak/keycloak:26.8.2@sha256:c68e286cacd87b942ac1939a0a1481461109d6acd3a57dc496969a319a76bbf0 AS keycloak-build
COPY --from=theme --chown=keycloak:keycloak /source/src/apps/keycloak-theme/dist_keycloak/teck-keycloak-theme.jar /opt/keycloak/providers/teck-keycloak-theme.jar
COPY --from=theme --chown=keycloak:keycloak /source/src/apps/keycloak-theme/LICENSE /opt/keycloak/providers/teck-keycloak-theme.LICENSE
RUN /opt/keycloak/bin/kc.sh build --db=postgres --health-enabled=true --metrics-enabled=true --features=opentelemetry
FROM quay.io/keycloak/keycloak:26.8.2@sha256:c68e286cacd87b942ac1939a0a1481461109d6acd3a57dc496969a319a76bbf0
COPY --from=keycloak-build /opt/keycloak/ /opt/keycloak/
USER 1000
ENTRYPOINT ["/opt/keycloak/bin/kc.sh"]
CMD ["start", "--optimized"]

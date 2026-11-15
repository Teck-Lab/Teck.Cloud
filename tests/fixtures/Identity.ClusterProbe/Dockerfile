# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:10.0.401@sha256:e70cdb7f80b0348f5cb85f19a8f670fca061f033d57eed12fa003d58b0e06317 AS build
WORKDIR /source
COPY global.json nuget.config Directory.Build.props Directory.Packages.props stylecop.json .editorconfig ./
COPY tests/fixtures/Identity.ClusterProbe ./tests/fixtures/Identity.ClusterProbe
COPY tests/e2e/web/Identity.E2ETests/IdentityProvisioner.cs ./tests/e2e/web/Identity.E2ETests/IdentityProvisioner.cs
COPY tests/fixtures/Identity.TestHost/Assets/index.html ./tests/fixtures/Identity.TestHost/Assets/index.html
RUN dotnet restore tests/fixtures/Identity.ClusterProbe/Identity.ClusterProbe.csproj --locked-mode
RUN dotnet publish tests/fixtures/Identity.ClusterProbe/Identity.ClusterProbe.csproj -c Release --no-restore --no-self-contained -o /app
FROM mcr.microsoft.com/dotnet/aspnet:10.0.15@sha256:bfd912c83295ce66d4b706ebb8541b9f14259b0e76fbb88b2751f58ce2399d2e
USER root
RUN apt-get update && apt-get install --no-install-recommends -y libnss3-tools ca-certificates fonts-liberation && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /home/app/.pki/nssdb && chown -R "$APP_UID:$APP_UID" /home/app
WORKDIR /app
COPY --from=build /app ./
ENV PLAYWRIGHT_BROWSERS_PATH=/ms-playwright
RUN chmod -R a+rX /app/.playwright && chmod a+rx /app/.playwright/node/linux-x64/node \
    && node_path=/app/.playwright/node/linux-x64/node \
    && "$node_path" /app/.playwright/package/cli.js install --with-deps --only-shell chromium \
    && chmod -R a+rX /ms-playwright && rm -rf /var/lib/apt/lists/*
COPY --chmod=0755 tests/fixtures/Identity.ClusterProbe/run.sh /app/run.sh
ENV HOME=/home/app NODE_EXTRA_CA_CERTS=/certs/ca.crt SSL_CERT_FILE=/certs/ca.crt
USER $APP_UID
RUN /app/.playwright/node/linux-x64/node --version && dotnet /app/Identity.ClusterProbe.dll --browser-smoke
ENTRYPOINT ["/app/run.sh"]

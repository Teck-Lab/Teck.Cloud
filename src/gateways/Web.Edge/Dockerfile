# syntax=docker/dockerfile:1.7

# Multi-stage Dockerfile for Web.Edge
# Optimized for multi-platform, Docker cache, and best practices

# Build arguments for .NET image version support
ARG DOTNET_SDK_VERSION=10.0.103
ARG DOTNET_RUNTIME_VERSION=10.0.3
ARG DOTNET_ALPINE_VERSION=3.23

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_SDK_VERSION}-alpine${DOTNET_ALPINE_VERSION} AS build
ARG BUILD_CONFIGURATION=Release
ARG VERSION=1.0.0
WORKDIR /src

# Copy only project files and props for restore layer (maximizes cache)
COPY ["global.json", "."]
COPY ["nuget.config", "."]
COPY ["Directory.Packages.props", "."]
COPY ["Directory.Build.props", "."]
COPY ["src/Directory.Build.props", "src/"]
COPY ["src/buildingblocks/Directory.Build.props", "src/buildingblocks/"]
COPY ["src/gateways/Web.Edge/Web.Edge.csproj", "src/gateways/Web.Edge/"]
COPY ["src/aspire/Teck.Cloud.ServiceDefaults/Teck.Cloud.ServiceDefaults.csproj", "src/aspire/Teck.Cloud.ServiceDefaults/"]
COPY ["src/buildingblocks/SharedKernel.Core/SharedKernel.Core.csproj", "src/buildingblocks/SharedKernel.Core/"]
COPY ["src/buildingblocks/SharedKernel.Grpc.Contracts/SharedKernel.Grpc.Contracts.csproj", "src/buildingblocks/SharedKernel.Grpc.Contracts/"]
COPY ["src/buildingblocks/SharedKernel.Infrastructure/SharedKernel.Infrastructure.csproj", "src/buildingblocks/SharedKernel.Infrastructure/"]

# Use Docker Buildx and TARGETPLATFORM to select the correct RID for multi-platform builds
ARG TARGETPLATFORM
# Default to x64 if not set
ARG RUNTIME_IDENTIFIER=linux-musl-x64

# Map TARGETPLATFORM to .NET RID
RUN --mount=type=cache,target=/root/.nuget/packages \
    if [ "$TARGETPLATFORM" = "linux/arm64" ]; then export RID=linux-musl-arm64; else export RID=linux-musl-x64; fi && \
    dotnet restore "src/gateways/Web.Edge/Web.Edge.csproj" -r $RID

# Copy everything else and build
COPY . .
WORKDIR "/src/src/gateways/Web.Edge"

RUN --mount=type=cache,target=/root/.nuget/packages \
    if [ "$TARGETPLATFORM" = "linux/arm64" ]; then export RID=linux-musl-arm64; else export RID=linux-musl-x64; fi && \
    dotnet build "Web.Edge.csproj" -c $BUILD_CONFIGURATION -o /app/build -r $RID \
        /p:Version=$VERSION \
        /p:PublishReadyToRun=true \
        /p:TreatWarningsAsErrors=false \
        /p:RunAnalyzersDuringBuild=false \
        /p:EnforceCodeStyleInBuild=false

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
ARG VERSION=1.0.0
ARG TARGETPLATFORM
ARG RUNTIME_IDENTIFIER=linux-musl-x64
RUN --mount=type=cache,target=/root/.nuget/packages \
    if [ "$TARGETPLATFORM" = "linux/arm64" ]; then export RID=linux-musl-arm64; else export RID=linux-musl-x64; fi && \
    dotnet publish "Web.Edge.csproj" -c $BUILD_CONFIGURATION -o /app/publish -r $RID --no-restore --no-build \
        /p:Version=$VERSION \
        /p:PublishReadyToRun=true \
        /p:PublishTrimmed=true \
        /p:TreatWarningsAsErrors=false \
        /p:RunAnalyzersDuringBuild=false \
        /p:EnforceCodeStyleInBuild=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_RUNTIME_VERSION}-alpine${DOTNET_ALPINE_VERSION}-composite AS final
ARG DOTNET_SDK_VERSION
ARG DOTNET_RUNTIME_VERSION
ARG DOTNET_ALPINE_VERSION

# Build arguments for metadata
ARG VERSION=1.0.0

# OCI Image Format Specification labels
# https://github.com/opencontainers/image-spec/blob/main/annotations.md
LABEL org.opencontainers.image.title="Web Edge Gateway" \
      org.opencontainers.image.description="Edge gateway for Teck platform" \
      org.opencontainers.image.version="${VERSION}" \
      org.opencontainers.image.authors="Teck Team" \
      org.opencontainers.image.vendor="Teck" \
      org.opencontainers.image.licenses="MIT" \
    org.opencontainers.image.base.name="mcr.microsoft.com/dotnet/aspnet:${DOTNET_RUNTIME_VERSION}-alpine${DOTNET_ALPINE_VERSION}-composite"

# Additional custom labels
LABEL com.teck.service.name="web-edge" \
      com.teck.service.type="gateway" \
      com.teck.service.tier="edge" \
    com.teck.dotnet.version="${DOTNET_RUNTIME_VERSION}" \
    com.teck.dotnet.sdk.version="${DOTNET_SDK_VERSION}" \
      com.teck.build.configuration="Release"

# Environment variables for .NET running in container
ENV APP_UID=1000 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    ASPNETCORE_HTTP_PORTS=8080

WORKDIR /app
RUN apk add --no-cache icu-libs
COPY --from=publish /app/publish .
EXPOSE 8080

# Set user only in final stage (if needed)
USER $APP_UID
ENTRYPOINT ["dotnet", "Web.Edge.dll"]
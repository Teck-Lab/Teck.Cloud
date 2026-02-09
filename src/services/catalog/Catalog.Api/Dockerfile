# Multi-stage Dockerfile for Catalog.Api
# Optimized for multi-platform, Docker cache, and best practices

# Build arguments for .NET version support
ARG DOTNET_VERSION=10.0

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION}-alpine AS build
ARG BUILD_CONFIGURATION=Release
ARG VERSION=1.0.0
WORKDIR /src

# Copy only project files and props for restore layer (maximizes cache)
COPY ["Directory.Packages.props", "."]
COPY ["Directory.Build.props", "."]
COPY ["src/services/Directory.Build.props", "src/services/"]
COPY ["src/services/catalog/Directory.Build.props", "src/services/catalog/"]
COPY ["src/buildingblocks/Directory.Build.props", "src/buildingblocks/"]
COPY ["src/services/catalog/Catalog.Api/Catalog.Api.csproj", "src/services/catalog/Catalog.Api/"]
COPY ["src/aspire/Teck.Cloud.ServiceDefaults/Teck.Cloud.ServiceDefaults.csproj", "src/aspire/Teck.Cloud.ServiceDefaults/"]
COPY ["src/buildingblocks/SharedKernel.Core/SharedKernel.Core.csproj", "src/buildingblocks/SharedKernel.Core/"]
COPY ["src/buildingblocks/SharedKernel.Infrastructure/SharedKernel.Infrastructure.csproj", "src/buildingblocks/SharedKernel.Infrastructure/"]
COPY ["src/buildingblocks/SharedKernel.Persistence/SharedKernel.Persistence.csproj", "src/buildingblocks/SharedKernel.Persistence/"]
COPY ["src/buildingblocks/SharedKernel.Secrets/SharedKernel.Secrets.csproj", "src/buildingblocks/SharedKernel.Secrets/"]
COPY ["src/services/catalog/Catalog.Application/Catalog.Application.csproj", "src/services/catalog/Catalog.Application/"]
COPY ["src/buildingblocks/SharedKernel.Events/SharedKernel.Events.csproj", "src/buildingblocks/SharedKernel.Events/"]
COPY ["src/services/catalog/Catalog.Domain/Catalog.Domain.csproj", "src/services/catalog/Catalog.Domain/"]
COPY ["src/services/catalog/Catalog.Infrastructure/Catalog.Infrastructure.csproj", "src/services/catalog/Catalog.Infrastructure/"]

# Use Docker Buildx and TARGETPLATFORM to select the correct RID for multi-platform builds
ARG TARGETPLATFORM
# Default to x64 if not set
ARG RUNTIME_IDENTIFIER=linux-musl-x64

# Map TARGETPLATFORM to .NET RID
RUN if [ "$TARGETPLATFORM" = "linux/arm64" ]; then export RID=linux-musl-arm64; else export RID=linux-musl-x64; fi && \
    dotnet restore "src/services/catalog/Catalog.Api/Catalog.Api.csproj" -r $RID

# Copy everything else and build
COPY . .
WORKDIR "/src/src/services/catalog/Catalog.Api"

# Build arguments for codegen (optional, with defaults)
ARG POSTGRES_CONNECTION="Host=localhost;Database=catalog;Username=postgres;Password=postgres"
ARG RABBITMQ_CONNECTION="amqp://guest:guest@localhost:5672/"

RUN if [ "$TARGETPLATFORM" = "linux/arm64" ]; then export RID=linux-musl-arm64; else export RID=linux-musl-x64; fi && \
    dotnet build "Catalog.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build -r $RID \
        /p:Version=$VERSION \
        /p:PublishReadyToRun=true \
        /p:TreatWarningsAsErrors=false \
        /p:RunAnalyzersDuringBuild=false \
        /p:EnforceCodeStyleInBuild=false

# Generate Wolverine codegen
ENV ASPNETCORE_ENVIRONMENT=Development \
    ConnectionStrings__postgres-write="${POSTGRES_CONNECTION}" \
    ConnectionStrings__rabbitmq="${RABBITMQ_CONNECTION}"
RUN dotnet /app/build/Catalog.Api.dll codegen write

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
ARG VERSION=1.0.0
ARG TARGETPLATFORM
ARG RUNTIME_IDENTIFIER=linux-musl-x64
RUN if [ "$TARGETPLATFORM" = "linux/arm64" ]; then export RID=linux-musl-arm64; else export RID=linux-musl-x64; fi && \
    dotnet publish "Catalog.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish -r $RID --self-contained true --no-restore \
        /p:Version=$VERSION \
        /p:UseAppHost=true \
        /p:PublishReadyToRun=true \
        /p:PublishTrimmed=true \
        /p:TreatWarningsAsErrors=false \
        /p:RunAnalyzersDuringBuild=false \
        /p:EnforceCodeStyleInBuild=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
ARG DOTNET_VERSION
FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION}-alpine-composite AS final

# Build arguments for metadata
ARG VERSION=1.0.0

# OCI Image Format Specification labels
# https://github.com/opencontainers/image-spec/blob/main/annotations.md
LABEL org.opencontainers.image.title="Catalog API" \
      org.opencontainers.image.description="Catalog microservice API for Teck platform" \
      org.opencontainers.image.version="${VERSION}" \
      org.opencontainers.image.authors="Teck Team" \
      org.opencontainers.image.vendor="Teck" \
      org.opencontainers.image.licenses="MIT" \
      org.opencontainers.image.base.name="mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION}-alpine-composite"

# Additional custom labels
LABEL com.teck.service.name="catalog" \
      com.teck.service.type="api" \
      com.teck.service.tier="backend" \
      com.teck.dotnet.version="${DOTNET_VERSION}" \
      com.teck.build.configuration="Release"

# Environment variables for .NET running in container
ENV APP_UID=1000 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    ASPNETCORE_HTTP_PORTS=8080

WORKDIR /app
COPY --from=publish /app/publish .
EXPOSE 8080

# Set user only in final stage (if needed)
USER $APP_UID
ENTRYPOINT ["dotnet", "Catalog.Api.dll"]
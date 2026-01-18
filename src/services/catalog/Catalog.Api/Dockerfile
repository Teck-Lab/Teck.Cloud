# Multi-stage Dockerfile for Catalog.Api
# Optimized for multi-platform, multi-version, Docker cache, and best practices
# 
# Build Args:
#   DOTNET_VERSION: .NET version to use (default: 10.0)
#   BUILD_CONFIGURATION: Build configuration (default: Release)
#   TARGETPLATFORM: Target platform (linux/amd64, linux/arm64)
#   ENABLE_TIERED_COMPILATION: Enable tiered compilation (default: false for native)
#   ENABLE_INVARIANT_GLOBALIZATION: Use invariant globalization (default: true)

# Build arguments for .NET version support
ARG DOTNET_VERSION=10.0
ARG BUILD_CONFIGURATION=Release

# Build stage - Uses SDK image for compilation
FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION}-alpine AS build

# Performance: Use BuildKit cache mounts for NuGet packages
ARG BUILD_CONFIGURATION
ARG TARGETPLATFORM
ARG RUNTIME_IDENTIFIER=linux-musl-x64

# Performance: Set environment variables for build optimization
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 \
    DOTNET_NOLOGO=1 \
    DOTNET_GENERATE_ASPNET_CERTIFICATE=0 \
    NUGET_XMLDOC_MODE=skip

WORKDIR /src

# Copy only project files and props for restore layer (maximizes Docker layer cache)
COPY ["Directory.Packages.props", "."]
COPY ["src/services/catalog/Directory.Build.props", "src/services/catalog/"]
COPY ["src/Directory.Build.props", "src/"]
COPY ["Directory.Build.props", "."]
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

# Performance: Map TARGETPLATFORM to .NET RID and restore with locked mode
RUN --mount=type=cache,target=/root/.nuget/packages \
    if [ "$TARGETPLATFORM" = "linux/arm64" ]; then export RID=linux-musl-arm64; else export RID=linux-musl-x64; fi && \
    dotnet restore "src/services/catalog/Catalog.Api/Catalog.Api.csproj" \
        -r $RID \
        --locked-mode \
        --disable-parallel

# Copy source code
COPY . .

# Build stage
WORKDIR "/src/src/services/catalog/Catalog.Api"
RUN --mount=type=cache,target=/root/.nuget/packages \
    if [ "$TARGETPLATFORM" = "linux/arm64" ]; then export RID=linux-musl-arm64; else export RID=linux-musl-x64; fi && \
    dotnet build "Catalog.Api.csproj" \
        -c $BUILD_CONFIGURATION \
        -o /app/build \
        -r $RID \
        --no-restore \
        /p:PublishReadyToRun=true \
        /p:DebugType=none \
        /p:DebugSymbols=false

# Publish stage - Creates optimized output
FROM build AS publish
ARG BUILD_CONFIGURATION
ARG TARGETPLATFORM
ARG RUNTIME_IDENTIFIER=linux-musl-x64
ARG ENABLE_TIERED_COMPILATION=false
ARG ENABLE_INVARIANT_GLOBALIZATION=true

RUN --mount=type=cache,target=/root/.nuget/packages \
    if [ "$TARGETPLATFORM" = "linux/arm64" ]; then export RID=linux-musl-arm64; else export RID=linux-musl-x64; fi && \
    dotnet publish "Catalog.Api.csproj" \
        -c $BUILD_CONFIGURATION \
        -o /app/publish \
        -r $RID \
        --self-contained true \
        --no-restore \
        --no-build \
        /p:UseAppHost=true \
        /p:PublishReadyToRun=true \
        /p:PublishTrimmed=true \
        /p:PublishSingleFile=false \
        /p:TrimMode=partial \
        /p:EnableCompressionInSingleFile=true \
        /p:IncludeNativeLibrariesForSelfExtract=true \
        /p:DebugType=none \
        /p:DebugSymbols=false \
        /p:EnableRequestDelegateGenerator=true \
        /p:EnableConfigurationBindingGenerator=true \
        /p:JsonSerializerIsReflectionEnabledByDefault=false \
        /p:InvariantGlobalization=$ENABLE_INVARIANT_GLOBALIZATION \
        /p:TieredCompilation=$ENABLE_TIERED_COMPILATION \
        /p:TieredCompilationQuickJit=false \
        /p:ServerGarbageCollection=true \
        /p:ConcurrentGarbageCollection=true \
        /p:RetainVMGarbageCollection=false

# Runtime stage - Minimal production image
ARG DOTNET_VERSION
FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION}-alpine-composite AS final

# Performance: Install runtime dependencies and security updates
RUN apk upgrade --no-cache && \
    apk add --no-cache \
        ca-certificates \
        tzdata \
        libgcc \
        libssl3 \
        libcrypto3 \
        zlib && \
    rm -rf /var/cache/apk/*

# Security: Create non-root user
RUN addgroup -g 1000 appgroup && \
    adduser -u 1000 -G appgroup -s /bin/sh -D appuser

# Performance: Set runtime environment variables
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1 \
    DOTNET_EnableDiagnostics=0 \
    DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 \
    DOTNET_NOLOGO=1 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_VERSION=${DOTNET_VERSION} \
    ASPNETCORE_URLS=http://+:8080;http://+:8081 \
    ASPNETCORE_HTTP_PORTS=8080;8081 \
    DOTNET_GCDynamicAdaptationMode=1 \
    DOTNET_gcServer=1 \
    DOTNET_GCConserveMemory=5 \
    COMPlus_EnableDiagnostics=0

WORKDIR /app

# Copy published files
COPY --from=publish --chown=appuser:appgroup /app/publish .

# Security: Set proper permissions
RUN chmod -R 550 /app && \
    chmod 770 /app

# Expose ports
EXPOSE 8080 8081

# Health check (optional - uncomment if you have a health endpoint)
# HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
#     CMD wget --no-verbose --tries=1 --spider http://localhost:8080/health || exit 1

# Security: Switch to non-root user
USER appuser

# Performance: Use native executable
ENTRYPOINT ["./Catalog.Api"]

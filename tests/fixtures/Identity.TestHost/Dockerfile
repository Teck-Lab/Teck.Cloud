# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:10.0.401@sha256:e70cdb7f80b0348f5cb85f19a8f670fca061f033d57eed12fa003d58b0e06317 AS build
WORKDIR /source
COPY global.json nuget.config Directory.Build.props Directory.Packages.props stylecop.json .editorconfig ./
COPY src/shared ./src/shared
COPY tests/fixtures/Identity.TestHost ./tests/fixtures/Identity.TestHost
RUN dotnet restore tests/fixtures/Identity.TestHost/Identity.TestHost.csproj --locked-mode
RUN dotnet publish tests/fixtures/Identity.TestHost/Identity.TestHost.csproj -c Release --no-restore --no-self-contained -o /app
FROM mcr.microsoft.com/dotnet/aspnet:10.0.15@sha256:bfd912c83295ce66d4b706ebb8541b9f14259b0e76fbb88b2751f58ce2399d2e
WORKDIR /app
COPY --from=build /app ./
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Identity.TestHost.dll"]

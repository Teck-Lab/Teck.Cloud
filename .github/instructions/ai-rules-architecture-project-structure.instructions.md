---
description: 'Converted from docs/ai/rules/architecture/project-structure.md'
applyTo: 'src/**/*.cs,tests/**/*.cs'
---
# Project Structure

## Intent

Apply clean architecture boundaries across services and building blocks.

## Layer Direction

- Domain: core business model and rules.
- Application: feature use cases (commands/queries), handlers, interfaces.
- Infrastructure: external concerns, persistence, adapters.
- API: transport contracts/endpoints.

Dependencies must flow inward toward domain/application abstractions.

## Repository Layout

- `src/buildingblocks/SharedKernel.*`
- `src/services/catalog/{Catalog.Domain,Catalog.Application,Catalog.Infrastructure,Catalog.Api}`
- `src/services/customer/{Customer.Domain,Customer.Application,Customer.Infrastructure,Customer.Api}`
- `src/gateways/{Web.Edge,Web.Aggregate.Gateway}`
- `src/aspire/{Teck.Cloud.AppHost,Teck.Cloud.ServiceDefaults}`
- `tests/{unit,integration,architecture,e2e}`

## Service Layout

- `src/services/{service}/{Service}.Domain`
- `src/services/{service}/{Service}.Application`
- `src/services/{service}/{Service}.Infrastructure`
- `src/services/{service}/{Service}.Api`

## Application Conventions

- Use feature-first folders: `<Aggregate>/Features/<UseCase>/V1`.
- Keep response models in `<Aggregate>/Responses`.
- Keep read models in `<Aggregate>/ReadModels` and repository interfaces in `<Aggregate>/Repositories`.
- Keep domain event handlers in `<Aggregate>/EventHandlers/DomainEvents`.
- For gRPC handlers, folder names can be `Grpc/gRpc/V1`, but namespaces should remain `*.Grpc.V1`.

## Conventions

- Keep business logic out of API and infrastructure edges.
- Keep repositories/interfaces in application/domain boundaries and implementations in infrastructure.
- Keep shared cross-cutting concerns in `src/buildingblocks/*`.

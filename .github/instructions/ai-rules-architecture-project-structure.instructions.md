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
- API: transport endpoints only.

Dependencies must flow inward toward domain/application abstractions.

## Repository Layout

- `src/buildingblocks/SharedKernel.*`
- `src/services/basket/{Basket.Domain,Basket.Application,Basket.Infrastructure,Basket.Api}`
- `src/services/catalog/{Catalog.Domain,Catalog.Application,Catalog.Infrastructure,Catalog.Api}`
- `src/services/customer/{Customer.Domain,Customer.Application,Customer.Infrastructure,Customer.Api}`
- `src/services/order/{Order.Domain,Order.Application,Order.Infrastructure,Order.Api}`
- `src/gateways/{Web.Public.Gateway,Web.Admin.Gateway}`
- `src/aspire/{Teck.Cloud.AppHost,Teck.Cloud.ServiceDefaults}`
- `tests/{unit,integration,architecture,e2e}`

## Service Layout

- `src/services/{service}/{Service}.Domain`
- `src/services/{service}/{Service}.Application`
- `src/services/{service}/{Service}.Infrastructure`
- `src/services/{service}/{Service}.Api`

## Application Conventions

- Use capability-first folders: `<Capability>/Features/<UseCase>/V1`.
- Keep `Request` and `Validator` types inside the same versioned use-case folder.
- Keep response models in capability folders (for example `<Capability>/Responses`).
- Keep read models in `<Capability>/ReadModels` and repository interfaces in `<Capability>/Repositories`.
- Keep domain event handlers in `<Capability>/EventHandlers/DomainEvents`.
- Do not use root-level `Application/Features/*` folders.
- For gRPC handlers, folder names can be `Grpc/gRpc/V1`, but namespaces should remain `*.Grpc.V1`.

## API Conventions

- Keep endpoint classes in API projects.
- Do not define `*Request` or `*Validator` types in API namespaces.
- API endpoints may reference Application request/validator types directly.

## Enforced Rules

- `tests/architecture/shared/Teck.Cloud.Arch.Tests/ApiThinSliceBoundaryTests.cs` enforces:
  - API projects do not contain request/validator types.
  - Application projects do not contain endpoint types.
  - No `<Service>.Application.Features.*` root namespace usage.
  - Feature request/validator types use versioned namespaces (`.Features.<UseCase>.Vn`).

## Conventions

- Keep business logic out of API and infrastructure edges.
- Keep repositories/interfaces in application/domain boundaries and implementations in infrastructure.
- Keep shared cross-cutting concerns in `src/buildingblocks/*`.

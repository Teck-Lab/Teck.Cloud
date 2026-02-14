# Project Structure

## Intent

Apply clean architecture boundaries across services and building blocks.

## Layer Direction

- Domain: core business model and rules.
- Application: use cases, commands/queries, interfaces.
- Infrastructure: external concerns, persistence, adapters.
- API: transport contracts/endpoints.

Dependencies must flow inward toward domain/application abstractions.

## Service Layout

- `src/services/{service}/{Service}.Domain`
- `src/services/{service}/{Service}.Application`
- `src/services/{service}/{Service}.Infrastructure`
- `src/services/{service}/{Service}.Api`

## Conventions

- Keep business logic out of API and infrastructure edges.
- Keep repositories/interfaces in application/domain boundaries and implementations in infrastructure.
- Keep shared cross-cutting concerns in `src/buildingblocks/*`.

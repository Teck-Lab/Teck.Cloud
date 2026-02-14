# Data Access Patterns

## Intent

Use CQRS-oriented read/write separation with repository abstractions and tenant-aware resolution.

## Rules

- Keep command-side writes on domain models and write repositories.
- Keep query-side reads optimized for read models/DTOs.
- Place repository interfaces in application/domain boundary projects.
- Place repository implementations and EF configuration in infrastructure.
- Keep `DbContext` ownership in infrastructure and wire via DI.
- For multi-tenant services, use shared tenant-resolution extensions and resolvers.

## Consistency

- Keep transaction boundaries explicit.
- Avoid bypassing application abstractions for direct data access from API layer.

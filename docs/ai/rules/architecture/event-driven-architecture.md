# Event-Driven Architecture

## Intent

Separate internal domain signaling from cross-service integration messaging.

## Domain Events

- Raised by aggregates for internal consistency.
- Handled within service boundaries.
- Keep payloads focused on domain intent and identifiers.

## Integration Events

- Used for asynchronous communication across services.
- Contracts live in shared event contracts where needed.
- Handlers must be idempotent and failure-aware.

## Rules

- Do not leak transport concerns into domain model.
- Log event publication and handling with correlation identifiers.
- Apply retry/dead-letter policies through messaging infrastructure.

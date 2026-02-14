# Inter-Service Communication

## Intent

Use the right communication mode per interaction type.

## Rules

- Gateway (BFF) to downstream services: HTTP.
- Direct synchronous service-to-service calls: gRPC when appropriate.
- Asynchronous cross-service propagation: integration events via broker.
- Resolve service locations via service discovery/config, not hardcoded addresses.

## Reliability and Security

- Propagate auth context safely and validate per service.
- Apply timeouts/retries/circuit breaking through configured clients.
- Handle and map transport errors explicitly.
- Emit logs/traces for all cross-service calls.

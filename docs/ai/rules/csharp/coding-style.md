# C# Coding Style

## Intent

Write readable, maintainable, modern C# with low coupling and explicit behavior.

## Rules

- Prefer clear domain naming and self-documenting code.
- Use records for immutable data shapes and value semantics where appropriate.
- Make classes `sealed` by default; only open inheritance intentionally.
- Prefer value objects over primitive obsession.
- Use pattern matching and expression-based code when it improves clarity.
- Keep functions pure where possible; isolate side effects.
- Minimize constructor dependencies; split responsibilities if dependency count grows.
- Prefer safe APIs (`TryParse`, `TryGetValue`) over exception-driven control flow.
- Flow `CancellationToken` through async call chains.
- Avoid `async void`, `.Result`, `.Wait()`, and `ContinueWith` for app logic.
- Enable and honor nullable reference types.
- Use `nameof(...)` for parameter/property/symbol references.
- Use result/error types for expected failures; exceptions for truly exceptional cases.

## Repository Alignment

- Follow `.editorconfig`, `stylecop.json`, and shared build properties.
- Keep changes minimal and bounded to the task scope.

# Testing Guidelines

## Intent

Keep tests deterministic, maintainable, and meaningful for change risk.

## Rules

- Use xUnit for unit/integration tests.
- Follow Arrange-Act-Assert structure.
- Use clear test names: `Method_WhenCondition_ExpectedResult`.
- Cover happy path, validation path, and failure path for changed logic.
- Prefer theory-based parametrized tests when cases differ only by input/output.
- Keep tests isolated; avoid shared mutable state.
- Dispose external resources and fixtures correctly.
- For infrastructure integration tests, prefer real dependencies (e.g., Testcontainers) over heavy mocking.
- Use built-in xUnit assertions unless project conventions indicate otherwise.

## Coverage

- Add/adjust tests with every behavior change.
- Prioritize coverage on domain and application logic.
- Follow effective CI thresholds and coverage scripts defined in repository workflows.

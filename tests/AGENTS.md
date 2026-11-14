# Tests: choose the smallest real boundary

Read [testing](../docs/testing.md) and [test tooling](../docs/test-tooling.md) when
adding/changing a suite. Follow the child AGENTS.md for the lane being edited.

- Unit and architecture: test. Integration: test:integration. Application E2E:
  test:e2e. Deployed Kubernetes: test:kubernetes. Use repository Nx/Bun commands.
- Work RED/GREEN on behavior, then verify integration and assembled acceptance.
  Mocking is a unit technique, not proof of database/transport correctness.
- Use Shouldly, NSubstitute interface mocks and the existing fixture generators.
  Keep assertion-relevant IDs/permissions/time/boundaries explicit; local seeds
  and isolated resource identities must remain safe under parallel execution.
- Keep xUnit/MTP and coverlet.MTP aligned. Do not add legacy VSTest/collector
  packages or relax analyzers for tests.
- Read [coverage](../docs/coverage.md) before baseline changes. Compare equivalent
  full scope; never lower baselines, exclude difficult code or remove assertions
  to obtain a pass.
- New features need actual behavioral tests. Record runtime blockers honestly;
  readiness or an empty runner result is not application acceptance evidence.
- Mobile testing remains undecided.

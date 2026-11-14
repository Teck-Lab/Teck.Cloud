# Shared TypeScript packages

Read [architecture](../../docs/architecture.md) for project boundaries and graph rules.

- Give each real package its own package.json and explicit workspace:* dependencies.
  Keep application business orchestration and platform-specific UI in its owner.
- Declare supported web/native platforms deliberately; avoid framework/native
  imports in neutral contracts and logic.
- Buildable exports point to compiled JS and matching declarations. Source-only
  packages may have typecheck without build; verify actual inferred Nx targets.
- Generated clients depend on their contract producer through explicit Nx edges.
  Do not edit generated output instead of the owning schema/generator.
- Test shared behavior and affected consumers. Add actual test/coverage wiring with
  the first package, following [testing](../../docs/testing.md) and
  [coverage](../../docs/coverage.md); do not install a mobile runner implicitly.
- Run Nx sync after dependency changes and verify affected selection in both
  directions. Keep Bun's lockfile and Nx plugin versions aligned.

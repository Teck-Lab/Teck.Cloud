# Test fixtures and test-only hosts

Read [test tooling](../../docs/test-tooling.md).

- Reuse production registration and real middleware when testing their behavior.
  Keep test-only endpoints, dependency setup and fake external boundaries here.
- Never ship fixture hosts as applications or reference them from production code.
- Keep configuration, test identities and resource lifetimes isolated. Dispose
  only resources the fixture creates.
- Do not disable auth, tenant filters, transaction middleware or read-only guards
  to make an integration claim pass.
- Document substituted boundaries. Tests needing the entire participating stack
  belong in E2E rather than an ever-growing universal fixture.

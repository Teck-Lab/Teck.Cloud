# Web and mobile applications

For a new app or project-graph change, read [architecture](../../docs/architecture.md).
For test design read [testing](../../docs/testing.md).

- Each app owns its package.json, framework dependencies and Nx targets. Bun owns
  installation/lockfile; use workspace:* for internal packages.
- Keep Next.js web and React Native platform code separate. Share neutral logic or
  contracts through src/packages; do not leak native modules into web bundles.
- Add the matching Nx framework plugin with the first actual app. Preserve version
  alignment and verify inferred targets and affected consumers.
- Use the customer/admin gateway contract; do not bypass it to reach service
  databases or internal implementation APIs. Treat the server as the auth/tenant
  authority and keep secrets out of browser/mobile bundles.
- Web unit tests stay focused. Actual browser journeys use Playwright with the
  Aspire-assembled stack; read tests/e2e/web/AGENTS.md when adding those tests.
- Mobile testing is undecided. Do not select a runner or fabricate a suite.
- Register runtime/generated-client dependencies in the Nx graph. Do not claim an
  empty target selection tested the app.

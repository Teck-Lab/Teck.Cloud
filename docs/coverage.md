# Coverage enforcement

`bun run coverage` builds Release and runs all .NET unit and integration projects
with coverlet.MTP. It merges line/branch hits across suites instead of averaging
percentages or adding duplicated lines. Scope is discovered from every source
project under `src/shared` and `src/services`; a new uninstrumented assembly fails.
Third-party assemblies, test fixtures and AppHost orchestration are outside that
business-code metric. Aspire and Kubernetes have separate behavioral E2E checks.
No source-file or branch exclusions are added to obtain a passing number.

The measured per-assembly baseline is `tools/coverage-baseline.json`. Both line
and branch coverage must not decline. On CI, the committed baseline is also
compared with the Nx base revision, preventing a PR from lowering the baseline
to conceal a regression. An intentionally removed assembly requires an explicit
policy migration, rather than disappearing silently from measurement.

When coverage improves, run `bun run coverage:update` and include the increased
baseline. The update command itself refuses decreases. Values are truncated to
three decimal percentage points to avoid insignificant formatting differences.
New code must bring appropriate tests; this gate is a ratchet, not a claim that
the initial shared libraries already have ideal coverage. Feature-specific
acceptance and meaningful assertions remain necessary.

Reports and the merged summary go to `.artifacts/coverage`, uploaded by CI.
The full measurement deliberately runs even though normal builds/E2E use Nx
selection: comparing affected-only coverage against a full baseline is invalid.
Future TypeScript applications need their own actual test runner and coverage
configuration; the current gate does not pretend to cover nonexistent apps.

`bun run test:tooling` exercises report merging and rejection of missing assemblies,
line regressions and branch regressions. Do not change measurement scope, build
configuration or test selection between baseline and candidate comparisons.

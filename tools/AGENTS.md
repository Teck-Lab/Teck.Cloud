# Repository tooling

Read [CI](../docs/ci.md) for workflow contracts, [coverage](../docs/coverage.md)
for coverage tools and [quality](../docs/dotnet-quality.md) for .NET policy validation.

- Prefer native Nx/language tooling over another build system. Keep commands
  compatible with Windows development and Linux CI; handle paths/quoting explicitly.
- Treat external inputs as data. Do not leak credentials or invoke a shell with
  untrusted interpolated text. Use bounded operations and meaningful failure codes.
- Coverage merges hits across suites, rejects missing assemblies/regressions and
  compares baseline revisions. Do not average percentages or narrow measurement.
- Security tooling fails on actual findings and operational errors; an unavailable
  scanner is not a clean result.
- Test behavioral changes, including rejection/error paths, with node:test in
  tools/*.test.mjs. Run bun run test:tooling and the relevant real command.
- Keep generated reports under .artifacts and out of source. Align workflow calls
  and documentation when interfaces change.

# Benchmarking

## Intent

Use reproducible benchmarks to measure performance and prevent regressions.

## Rules

- Use BenchmarkDotNet for microbenchmarks.
- Run benchmarks in `Release` with optimized builds.
- Include memory/allocation diagnostics where relevant.
- Isolate benchmark setup from measured operations.
- Compare baseline vs candidate implementations explicitly.
- Persist benchmark artifacts in CI when running performance checks.

## Scope

- Use benchmarks for performance-sensitive code paths, not as unit-test replacements.

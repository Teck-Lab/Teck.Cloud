# Dockerfile Patterns

## Intent

Keep service images reproducible, secure, and minimal, with consistent multi-stage builds.

## Rules

- Use multi-stage builds (`build`, `publish`, `final`).
- Pin to repository-supported .NET major version (currently .NET 10 unless project-specific override).
- Keep runtime image minimal and run as non-root.
- Preserve deterministic publish output and explicit output directories.
- Use layer caching effectively: copy project/props first, then source.

## Wolverine Codegen Services

- For services requiring code generation during image build, run codegen from built output with required env/config set.
- Do not rely on implicit `dotnet run` behavior for codegen in container builds.

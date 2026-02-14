# Consuming dotnet Tools

## Intent

Ensure reproducible local and CI tool usage through a committed tool manifest.

## Rules

- Keep `.config/dotnet-tools.json` in source control.
- Install tools with explicit versions.
- Restore tools with `dotnet tool restore` in local setup and CI.
- Periodically remove unused/deprecated tools and update intentionally.

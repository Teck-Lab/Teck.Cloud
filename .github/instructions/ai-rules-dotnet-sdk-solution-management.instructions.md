---
description: 'Converted from docs/ai/rules/dotnet-sdk/solution-management.md'
applyTo: '**/*.sln,**/*.slnx,**/*.csproj,global.json,Directory.Packages.props,Directory.Build.props,nuget.config'
---
# .NET Solution Management

## Intent

Maintain consistent builds and dependency behavior across all projects.

## Rules

- Pin SDK in `global.json`.
- Centralize shared properties in `Directory.Build.props`.
- Centralize package versions in `Directory.Packages.props`.
- Use `nuget.config` with explicit package source mapping.
- Prefer `dotnet` CLI for deterministic local/CI workflows.

## Build Quality

- Keep warning/error policy consistent with repository settings.
- Use deterministic CI build flags where configured.

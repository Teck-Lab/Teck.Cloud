---
description: 'Converted from docs/ai/rules/dotnet-sdk/dependency-management.md'
applyTo: '**/*.sln,**/*.slnx,**/*.csproj,global.json,Directory.Packages.props,Directory.Build.props,nuget.config'
---
# Dependency Management

## Intent

Keep dependencies secure, compliant, and maintainable.

## Rules

- Add/update dependencies through `dotnet` CLI and centralized package management.
- Prefer explicit versions and controlled upgrades.
- Regularly scan for vulnerable packages.
- Validate package license compatibility for new additions.
- Run restore/build/test after dependency updates.

## Operational Commands

- `dotnet list package --outdated`
- `dotnet list package --vulnerable`
- `dotnet restore`
- `dotnet build`
- `dotnet test`

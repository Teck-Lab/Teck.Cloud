# Publishing dotnet Tools

## Intent

Publish stable, well-documented tools with explicit compatibility and secure metadata.

## Rules

- Set `<PackAsTool>true</PackAsTool>` and complete NuGet package metadata.
- Use semantic versioning.
- Provide usage docs and inline help.
- Target repository baseline runtime unless the tool intentionally multi-targets.
- Validate pack/install/run behavior before publishing.

# Contributing to Teck.Cloud

Thank you for your interest in contributing to Teck.Cloud! This document provides guidelines for contributing to this .NET 10 microservices platform.

## Getting Started

1. Fork the repository and clone it locally.
2. Ensure you have .NET 10 SDK installed (`dotnet --version`).
3. Run `dotnet restore` and `dotnet build` to verify your environment.

## Development Workflow

1. Create a feature branch from `main`: `git checkout -b feature/your-feature-name`
2. Make your changes following the repository conventions.
3. Add or update tests for any behavior changes.
4. Run the full test suite: `dotnet test`.
5. Ensure your code follows the style rules (enforced by `.editorconfig` and `stylecop.json`).

## Conventions

- Follow Clean Architecture boundaries: Domain ← Application ← Infrastructure ← API.
- Keep API projects transport-thin — endpoint classes only.
- Place request/validator types in Application feature slices.
- Use capability-first folders: `<Capability>/Features/<UseCase>/V1`.
- Follow C# 14 conventions: `sealed` classes by default, records for immutable data.
- Flow `CancellationToken` through all async chains.

## Testing

- Use xUnit with Arrange-Act-Assert structure.
- Name tests: `Method_WhenCondition_ExpectedResult`.
- Integration tests prefer real dependencies (Testcontainers).
- Architecture tests enforce layer boundaries.

## Pull Request Process

1. Ensure CI passes (build, test, security scans).
2. Request review from `@teck-lab/platform-team`.
3. Address review feedback promptly.
4. Squash commits before merging if requested.

## Questions?

Open an issue or reach out to the platform team.

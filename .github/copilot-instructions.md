# Teck.Cloud Copilot Instructions

This repository uses the rule set under `docs/ai/rules` as the canonical AI coding guidance.

## Instruction Precedence

1. Repository source of truth and existing code patterns
2. `Directory.Build.props`, `Directory.Packages.props`, `global.json`, `nuget.config`, `stylecop.json`, `.editorconfig`
3. `docs/ai/rules/README.md` and linked rule files
4. Task-specific user instructions

## Core Expectations

- Follow clean architecture boundaries used in `src/services/*`.
- Prefer minimal, safe changes; do not refactor unrelated code.
- Keep tests aligned with project conventions in `tests/` and CI workflows.
- Use .NET 10 conventions in this repository unless a project explicitly requires otherwise.

## Canonical Rule Set

- `docs/ai/rules/README.md`

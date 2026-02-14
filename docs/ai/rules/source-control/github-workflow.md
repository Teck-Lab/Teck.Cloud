# GitHub Workflow and Releases

## Scope

This rule defines the repository Git workflow, commit message convention, and release branching model.

## Commit Messages

- Follow the Conventional Commits specification.
- Use clear commit types and scopes when applicable (for example: `feat(catalog): add tenant header forwarding`).
- Keep commit messages compatible with Auto-based release automation.

## Branching Strategy

- `main` is the stable branch and should always be deployable.
- New features should be developed on `feature/*` branches.
- Bug fixes should be developed on `fix/*` branches.
- When possible, feature and fix branches should reference a GitHub issue or GitHub Project/board task.

## Pull Requests

- Open pull requests from feature/fix branches into `main` (or `master` where applicable).
- Ensure PR descriptions and linked work items provide traceability to the originating issue/task.

## Pre-release Branches

Use the following branches for pre-release flows:

- `next`
- `next-major`
- `alpha`
- `beta`

## Canary and Automation

- Canary releases are automatically created when a pull request targets `main`/`master`.
- Release orchestration is handled by Auto; commit style and branch naming must remain compatible with Auto workflows.

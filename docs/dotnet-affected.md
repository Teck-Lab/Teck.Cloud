# dotnet-affected + Auto Integration

This repository uses [dotnet-affected](https://github.com/leonardochaia/dotnet-affected) to optimize CI/CD pipelines and [Intuit Auto](https://intuit.github.io/auto) for automated releases.

## How It Works

### dotnet-affected
Analyzes your project dependency graph and git history to determine:
1. **Which projects have changed** - Based on modified files
2. **Which NuGet packages have changed** - By analyzing `Directory.Packages.props`
3. **Which projects are affected** - By following project references and dependencies
4. **Which projects need to be built/tested/deployed** - Transitive closure of affected projects

### Auto (Intuit Auto)
Handles automated versioning and releases:
1. **Label-based versioning** - Uses PR labels to determine version bumps
2. **Changelog generation** - Automatically generates changelogs from PRs
3. **Multi-branch releases** - Supports main, next, alpha, beta branches
4. **Service-specific tags** - Creates tags like `catalog@v1.2.3`

## Workflow Overview

### Pull Request Workflow
```
1. Developer opens PR
2. dotnet-affected detects changed projects
3. Only affected projects are built and tested
4. PR comment shows which projects are affected
5. Developer adds labels (major/minor/patch/etc.)
```

### Release Workflow
```
1. PR merged to main/next/alpha/beta
2. dotnet-affected detects which services changed
3. Auto runs for each affected service
4. Auto calculates version based on PR labels
5. Creates release with changelog
6. Tags release (e.g., catalog@v1.2.3)
7. Docker builds triggered by tag
```

## Using Auto Labels

Auto uses PR labels to determine version bumps:

### Version Labels

| Label | Version Bump | Use When |
|-------|-------------|----------|
| `major` | 1.0.0 → 2.0.0 | Breaking changes |
| `minor` | 1.0.0 → 1.1.0 | New features (backward compatible) |
| `patch` | 1.0.0 → 1.0.1 | Bug fixes |

### Special Labels

| Label | Behavior | Use When |
|-------|----------|----------|
| `skip-release` | No release created | WIP changes |
| `release` | Force release | Manual release trigger |
| `internal` | No version bump | Internal refactoring |
| `documentation` | No version bump | Docs only |
| `tests` | No version bump | Test updates only |
| `dependencies` | No version bump | Dependency updates |
| `performance` | Patch bump | Performance improvements |

### Example Workflow

```bash
# Create feature branch
git checkout -b feature/add-search

# Make changes to catalog service
# ... code changes ...

# Commit and push
git commit -m "Add product search functionality"
git push origin feature/add-search

# Create PR
gh pr create --title "Add product search" --label "minor"

# When merged to main:
# - Auto detects "minor" label
# - Bumps catalog from v1.0.0 → v1.1.0
# - Creates tag: catalog@v1.1.0
# - Generates changelog with PR details
# - Docker build triggered
```

## Service Configuration

Each service needs an `.autorc` configuration file.

### Creating Config for New Service

1. Create `.autorc` in service directory:

```bash
# For catalog service
cat > src/services/catalog/.autorc << 'EOF'
{
  "extends": "../../.autorc",
  "git-tag": {
    "tagPrefix": "catalog@"
  }
}
EOF
```

2. Replace `catalog@` with your service name (e.g., `inventory@`)

### Root Configuration

The root `.autorc` contains shared configuration:
- PR labels and their meanings
- Changelog titles
- Release branches (main, next, alpha, beta)
- Auto plugins

## Branch Strategy

### Main Branch
- Stable releases
- Version: `1.2.3`
- Tag: `catalog@v1.2.3`

### Next Branch  
- Pre-releases for upcoming version
- Version: `1.3.0-next.1`, `1.3.0-next.2`
- Tag: `catalog@v1.3.0-next.1`

### Alpha/Beta Branches
- Early testing releases
- Version: `1.2.0-alpha.1`, `1.2.0-beta.1`
- Tag: `catalog@v1.2.0-alpha.1`

## How Auto Handles Merges

### Merging `next` → `main`

```
Timeline:
1. next: PR with "minor" label → catalog@v1.1.0-next.1
2. next: PR with "patch" label → catalog@v1.1.0-next.2
3. Merge next → main
   → Auto finds last stable tag: catalog@v1.0.0
   → Analyzes PRs since v1.0.0
   → Creates: catalog@v1.1.0 (stable)
```

### After Hotfix on Main

```
1. main: Fix critical bug (patch) → catalog@v1.0.1
2. Always merge main → next after hotfixes!
   git checkout next
   git merge main
   # This keeps next in sync with main
```

## Example Scenarios

### Scenario 1: Adding a Feature

```bash
# On feature branch
git commit -m "feat: add product filtering"

# Create PR with label
gh pr create --label "minor"

# After merge to main
# Auto creates: catalog@v1.1.0
```

### Scenario 2: Breaking Change

```bash
# On feature branch  
git commit -m "feat!: change API response format"

# Create PR with label
gh pr create --label "major"

# After merge to main
# Auto creates: catalog@v2.0.0
```

### Scenario 3: Multiple Changes in PR

```bash
# PR has commits:
# - "fix: resolve bug"
# - "feat: add feature"
# - "docs: update readme"

# Add label: "minor" (highest impact)
gh pr edit --add-label "minor"

# After merge to main
# Auto creates: catalog@v1.1.0 (minor wins)
```

### Scenario 4: Pre-release on Next

```bash
# Merge PR to next branch (not main)
git checkout next
gh pr merge 123

# Auto creates: catalog@v1.1.0-next.1
```

## Creating Labels

Auto can automatically create the labels defined in `.autorc`:

```bash
# Run once per repository
auto create-labels
```

Or the workflow will create them automatically on first run.

## Local Testing

Test Auto locally before pushing:

```bash
# See what version would be created
cd src/services/catalog
auto version

# See what changelog would be generated  
auto changelog

# Dry run (won't create tags/releases)
GH_TOKEN=your-token auto shipit --dry-run
```

## Troubleshooting

### "No published yet" error

If Auto says the service hasn't been published yet:
1. Create an initial release manually: `gh release create catalog@v0.1.0`
2. Or let Auto create the first release (it will default to v1.0.0)

### Labels not working

1. Make sure labels exist: `auto create-labels`
2. Check PR has correct label before merging
3. Verify label name matches `.autorc` configuration

### Release not created

Possible reasons:
1. No PR labels (Auto defaults to patch)
2. `skip-release` label present
3. Commit message contains `[skip ci]`
4. No changes to service (dotnet-affected detected nothing)

### Wrong version bump

- Check which label was on the PR
- Auto uses the highest priority label
- Priority: major > minor > patch > none

## Advantages Over Semantic-Release

| Feature | Auto | Semantic-Release |
|---------|------|------------------|
| Commit message parsing | Optional (label-based) | Required |
| Version control | PR labels (visual) | Commit messages (hidden) |
| Multi-package monorepo | Excellent | Complex setup |
| Prerelease branches | Built-in | Manual config |
| Changelog quality | Rich (full PR context) | Basic (commit messages) |
| Learning curve | Low (labels are intuitive) | Medium (commit format strict) |

## Best Practices

### ✅ Do

- Add version labels to PRs before merging
- Merge PRs (don't squash) to preserve PR context
- Keep main → next synced after hotfixes
- Use `skip-release` for WIP merges
- Review changelog in release before publishing

### ❌ Don't

- Don't create version tags manually
- Don't merge without labels (defaults to patch)
- Don't squash merge (loses PR metadata)
- Don't skip `fetch-depth: 0` in workflows
- Don't use force push on release branches

## References

- [Auto Documentation](https://intuit.github.io/auto)
- [dotnet-affected Documentation](https://github.com/leonardochaia/dotnet-affected)
- [Conventional Commits](https://www.conventionalcommits.org/)
- [MSBuild Traversal SDK](https://github.com/microsoft/MSBuildSdks/tree/main/src/Traversal)

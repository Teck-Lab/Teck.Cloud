# Quick Reference: dotnet-affected + Auto

## For Developers

### Making Changes

Add labels to your PRs to control versioning:

```bash
# Create PR with version label
gh pr create --title "Add search feature" --label "minor"

# Add label to existing PR
gh pr edit 123 --add-label "patch"

# Multiple labels (highest wins)
gh pr edit 123 --add-label "minor" --add-label "documentation"
```

### Label Guide

| Label | Version | Example |
|-------|---------|---------|
| `major` | 1.0.0 → 2.0.0 | Breaking API changes |
| `minor` | 1.0.0 → 1.1.0 | New features |
| `patch` | 1.0.0 → 1.0.1 | Bug fixes |
| `skip-release` | No release | WIP changes |
| `documentation` | No version bump | Docs only |
| `tests` | No version bump | Test changes only |

### What Gets Built?

When you open a PR:
- ✅ Only affected projects are built and tested
- ✅ CI runs faster  
- ✅ PR comment shows which projects are affected
- ✅ Add labels to control release version

### Testing Locally

```bash
# See what's affected by your changes
dotnet affected describe

# Build only affected projects
dotnet affected --format traversal
dotnet build affected.proj

# Test only affected projects
dotnet test affected.proj

# See what version would be created (from service directory)
cd src/services/catalog
auto version

# See changelog preview
auto changelog
```

## For DevOps/CI Maintainers

### Workflow Files

| Workflow | Purpose | Trigger |
|----------|---------|---------|
| `dotnet-test-check.yaml` | PR validation with affected projects | Pull requests to `main` |
| `release-services.yaml` | Auto releases for affected services | Push to release branches |
| `docker-publish.yaml` | Docker image builds | Service tags (e.g., `catalog@v1.2.3`) |

### How Releases Work

1. **PR merged** with labels (e.g., `minor`)
2. **dotnet-affected** detects which services changed
3. **Auto** calculates version based on PR labels
4. **Creates release** with changelog and tag (e.g., `catalog@v1.2.3`)
5. **Docker build** triggers on new tag

### Creating Labels

Labels are auto-created on first run, or manually:

```bash
# From root or service directory
auto create-labels
```

### Configuration Files

```
.autorc                          # Root config (shared)
src/services/catalog/.autorc     # Service config
src/services/inventory/.autorc   # Another service
```

## Service Setup Checklist

Adding a new service?

- [ ] Create `src/services/{service}/.autorc`:
  ```json
  {
    "extends": "../../.autorc",
    "git-tag": {
      "tagPrefix": "inventory@"
    }
  }
  ```
- [ ] Create initial release: `gh release create inventory@v0.1.0`
- [ ] Test locally: `cd src/services/inventory && auto version`
- [ ] Merge a PR with a label to trigger first release

## Common Scenarios

### Scenario: Feature Development

```bash
# 1. Create feature branch
git checkout -b feature/search

# 2. Make changes, commit
git commit -m "Add product search"

# 3. Create PR with minor label
gh pr create --label "minor"

# 4. After merge → catalog@v1.1.0
```

### Scenario: Hotfix

```bash
# 1. On main branch
git checkout main

# 2. Fix bug, create PR
gh pr create --label "patch" --title "Fix critical bug"

# 3. After merge → catalog@v1.0.1

# 4. IMPORTANT: Sync to next!
git checkout next
git merge main
git push
```

### Scenario: Pre-release

```bash
# 1. Merge PR to next branch
git checkout next
gh pr merge 123

# 2. Auto creates → catalog@v1.1.0-next.1

# 3. More changes on next
gh pr merge 124

# 4. Auto creates → catalog@v1.1.0-next.2

# 5. Ready for production?
gh pr create --base main --head next
# After merge → catalog@v1.1.0
```

### Scenario: Breaking Change

```bash
# Create PR with major label
gh pr create --label "major" --title "Change API response format"

# After merge → catalog@v2.0.0
```

## Version Flow Examples

### Linear (Main only)

```
v1.0.0 → v1.0.1 (patch) → v1.1.0 (minor) → v2.0.0 (major)
```

### With Pre-releases

```
main:  v1.0.0 ─────────────────────────→ v1.1.0
                                          ↑
next:           v1.1.0-next.1 → v1.1.0-next.2 ──┘
```

### Multi-branch

```
main:  v1.0.0 ────→ v1.0.1 ────→ v1.1.0
                      ↓
next:                 v1.0.1 ────→ v1.1.0 ────→ v1.2.0-next.1
                                                 ↓
beta:                                            v1.2.0-beta.1
```

## Troubleshooting

| Problem | Solution |
|---------|----------|
| No release created | Check PR had a label (or add `release` label) |
| Wrong version bump | Check which label was used (major > minor > patch) |
| Release skipped | Look for `skip-release` label or `[skip ci]` in commit |
| Multiple services released | Changes affected shared buildingblocks |
| "Not published yet" error | Create initial release: `gh release create service@v0.1.0` |

## Label Priority

When PR has multiple labels:

```
major > minor > patch > none
```

Example:
- PR with `minor` + `documentation` → Minor release (v1.1.0)
- PR with `major` + `minor` → Major release (v2.0.0)
- PR with `skip-release` + `minor` → No release (skip-release wins)

## Useful Commands

```bash
# Create labels in repository
auto create-labels

# See current version
cd src/services/catalog
auto version

# Preview changelog
auto changelog

# Test release (dry run)
GH_TOKEN=$token auto shipit --dry-run

# Check what would be affected
dotnet affected --from origin/main --to HEAD --verbose

# Build only affected
dotnet affected --format traversal
dotnet build affected.proj
```

## Integration Flow

```
Developer → PR + Labels → Merge
                            ↓
                    dotnet-affected
                            ↓
                      Detect Services
                            ↓
                    Auto (per service)
                            ↓
                    Calculate Version
                            ↓
                  Generate Changelog
                            ↓
                    Create Tag & Release
                            ↓
                    Docker Build Trigger
```

## Best Practices

### ✅ Do
- Always add version labels to PRs
- Use merge commits (preserve PR context)
- Sync main → next after hotfixes
- Review auto-generated changelog
- Use `skip-release` for WIP merges

### ❌ Don't  
- Squash merge (loses PR metadata)
- Merge without labels
- Create tags manually
- Force push on main/next/alpha/beta
- Skip fetch-depth: 0 in CI

## Support

- Auto docs: https://intuit.github.io/auto
- dotnet-affected docs: https://github.com/leonardochaia/dotnet-affected
- Full guide: [`docs/dotnet-affected.md`](./dotnet-affected.md)

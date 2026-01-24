# dotnet-affected Integration

This repository uses [dotnet-affected](https://github.com/leonardochaia/dotnet-affected) to optimize CI/CD pipelines by only building, testing, and releasing projects that are affected by changes.

## How It Works

dotnet-affected analyzes your project dependency graph and git history to determine:

1. **Which projects have changed** - Based on modified files
2. **Which NuGet packages have changed** - By analyzing `Directory.Packages.props`
3. **Which projects are affected** - By following project references and dependencies
4. **Which projects need to be built/tested/deployed** - Transitive closure of affected projects

### Example

If you have this structure:
- `SharedKernel.Core` (buildingblock)
- `Catalog.Domain` (depends on SharedKernel.Core)
- `Catalog.Api` (depends on Catalog.Domain)
- `Inventory.Api` (independent service)

**Scenario 1:** You change `Catalog.Api`
- Only `Catalog.Api` needs to be built and tested

**Scenario 2:** You change `Catalog.Domain`
- Both `Catalog.Domain` and `Catalog.Api` need to be built and tested

**Scenario 3:** You change `SharedKernel.Core`
- All projects that depend on it need to be built and tested (Catalog.Domain, Catalog.Api, and any other dependent projects)

**Scenario 4:** You change `Inventory.Api`
- Only `Inventory.Api` needs to be built and tested (Catalog service is unaffected)

## Workflows Using dotnet-affected

### Pull Request Workflow (`dotnet-test-check.yaml`)

When you open a PR, the workflow:

1. **Detects affected projects** using `dotnet affected --from base --to head`
2. **Generates `affected.proj`** - A traversal project containing only affected projects
3. **Builds only affected projects** - `dotnet build affected.proj`
4. **Tests only affected projects** - `dotnet test affected.proj`
5. **Comments on PR** - Shows which projects will be built/tested
6. **Builds Docker images** - Only for affected projects with Dockerfiles

**Benefits:**
- Faster CI runs (only build what changed)
- Faster feedback loop for developers
- Reduced resource usage

### Release Workflow (`release-services.yaml`)

When you push to `main`/`next`/`alpha`/`beta`:

1. **Detects affected services** since the last successful release
2. **Runs semantic-release** for each affected service independently
3. **Creates service-specific tags** (e.g., `catalog@v1.2.3`)
4. **Generates changelogs** based on conventional commits
5. **Triggers Docker builds** via the `docker-publish.yaml` workflow

**Benefits:**
- Only release services that have changed
- Independent service versioning
- Proper semantic versioning per service

## Adding a New Service

To enable semantic-release for a new service:

1. Create `.releaserc.json` in your service directory (e.g., `src/services/inventory/.releaserc.json`)

```json
{
  "branches": [
    "+([0-9])?(.{+([0-9]),x}).x",
    "main",
    { "name": "next", "prerelease": true, "channel": "next" },
    "next-major",
    { "name": "beta", "prerelease": true, "channel": "beta" },
    { "name": "alpha", "prerelease": true, "channel": "alpha" }
  ],
  "tagFormat": "inventory@v${version}",
  "plugins": [
    [
      "@semantic-release/commit-analyzer",
      {
        "preset": "conventionalcommits",
        "releaseRules": [
          { "type": "feat", "scope": "inventory", "release": "minor" },
          { "type": "fix", "scope": "inventory", "release": "patch" },
          { "type": "perf", "scope": "inventory", "release": "patch" },
          { "type": "refactor", "scope": "inventory", "release": "patch" },
          { "type": "build", "scope": "inventory", "release": "patch" },
          { "breaking": true, "scope": "inventory", "release": "major" },
          { "scope": "inventory", "release": false }
        ]
      }
    ],
    [
      "@semantic-release/release-notes-generator",
      {
        "preset": "conventionalcommits",
        "presetConfig": {
          "types": [
            { "type": "feat", "section": "✨ Features" },
            { "type": "fix", "section": "🐛 Bug Fixes" },
            { "type": "perf", "section": "⚡ Performance Improvements" },
            { "type": "refactor", "section": "♻️ Code Refactoring" },
            { "type": "docs", "section": "📚 Documentation", "hidden": false },
            { "type": "test", "section": "✅ Tests", "hidden": false },
            { "type": "build", "section": "🏗️ Build System", "hidden": false },
            { "type": "ci", "section": "👷 CI/CD", "hidden": false }
          ]
        }
      }
    ],
    [
      "@semantic-release/exec",
      {
        "prepareCmd": "echo \"Preparing release ${nextRelease.version} for inventory service\""
      }
    ],
    [
      "@semantic-release/github",
      {
        "successComment": false,
        "failTitle": false,
        "releasedLabels": ["released<%= nextRelease.channel ? `-${nextRelease.channel}` : \"\" %>"]
      }
    ]
  ]
}
```

2. Update the `scope` and `tagFormat` to match your service name

## Commit Message Format

Use [Conventional Commits](https://www.conventionalcommits.org/) with the service scope:

```
feat(catalog): add product search endpoint
fix(inventory): resolve stock calculation bug
refactor(catalog): improve query performance
docs(catalog): update API documentation

feat(catalog)!: remove legacy endpoint
# or
feat(catalog): change API response format

BREAKING CHANGE: The API now returns ISO 8601 dates
```

**Scopes:**
- `catalog` - Catalog service
- `inventory` - Inventory service (when added)
- `buildingblocks` - Shared buildingblocks
- `ci` - CI/CD changes

## Local Testing

You can test which projects are affected locally:

```bash
# Restore dotnet tools
dotnet tool restore

# Check affected projects since last commit
dotnet affected --verbose

# Check affected projects between branches
dotnet affected --from main --to feature/my-branch

# Dry run to see what would be generated
dotnet affected --dry-run --verbose

# Assume changes to a specific project (useful for testing)
dotnet affected --assume-changes Catalog.Api

# Generate all output formats
dotnet affected --format text json traversal --output-dir ./output
```

The tool generates:
- `affected.proj` - MSBuild Traversal project
- `affected.txt` - Plain text list of project paths
- `affected.json` - JSON structured data

## Excluding Projects from Affected Analysis

To exclude certain projects (e.g., benchmarks, samples):

```bash
dotnet affected --exclude ".*Benchmarks.*" --exclude ".*Samples.*"
```

## Performance Tips

1. **Fetch depth**: The workflows use `fetch-depth: 0` to get full git history for accurate comparison
2. **Caching**: NuGet package caching is handled by GitHub Actions
3. **Concurrency**: Docker builds run in parallel for different architectures

## Troubleshooting

### "No projects affected" when you expect changes

Check:
- Git history is available (`fetch-depth: 0`)
- You're comparing the right commits
- Project files are properly referenced in the dependency graph

### Workflow says everything is affected

This usually happens when:
- Changes to `Directory.Build.props` or `Directory.Packages.props`
- Changes to shared buildingblocks
- First run (no previous baseline)

This is correct behavior - these files affect all projects.

## References

- [dotnet-affected Documentation](https://github.com/leonardochaia/dotnet-affected)
- [Semantic Release](https://semantic-release.gitbook.io/)
- [Conventional Commits](https://www.conventionalcommits.org/)
- [MSBuild Traversal SDK](https://github.com/microsoft/MSBuildSdks/tree/main/src/Traversal)

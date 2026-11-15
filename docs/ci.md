# Affected CI and releases

GitHub Actions uses Nx's dependency graph, full Git history and nrwl/nx-set-shas.
PRs compare against their base; main compares against the last successful CI run
so failed changes are not skipped. Without a successful baseline, select everything.

A short baseline job resolves the shared NX_BASE/NX_HEAD once. Independent jobs then
run build/fast checks, full unit/integration coverage, Aspire E2E, container builds
and a full/sandbox KIND matrix concurrently on separate runners. Each job checks
out full history and materializes the empty-tree object for first-run fallback.
The matrix does not cancel its other mode when one fails.

Workspace format/config and tooling checks always run. Build, lint, typecheck,
unit and architecture checks use affected selection. The fast lane intentionally
runs affected unit tests for early feedback; the coverage lane runs the complete
unit/integration set once to enforce the [coverage ratchet](coverage.md). There is
no additional integration lane repeating those real-container tests. Static
Wolverine verification belongs to the fast lane. CI supplies the Sentinel address
and installs pinned, checksum-verified KIND/kubectl/Helm in the Kubernetes jobs.
Aspire and KIND verify infrastructure and the test-only identity BFF/gRPC journey.
They do not claim a production business application. Browser-bearing integration
and static-handler lanes install the pinned Playwright Chromium before execution;
the Nx targets build the shared identity image before those real-container tests.

The final **Affected checks** job preserves the required-check name and requires
every lane, including both KIND matrix entries, to succeed. Failure, cancellation
or an unexpected skipped job cannot produce a green gate. All lanes use the same
affected baseline; test jobs still succeed normally when Nx selects no targets.
Evidence artifacts have distinct lane/mode names and upload even after failures.

Bun restores use the frozen lockfile. Every .NET CI restore uses locked mode,
including CodeQL and the candidate builder; pack reuses the restored graph with
--no-restore. CI-wide MSBuild policy also protects implicit restores. Dependency
updates must regenerate and review the affected packages.lock.json files.

CI records affected projects and image projects as artifacts, and builds affected
container:build targets. Shared .NET/TS changes select their transitive consumers.
Declare graph edges for codegen, runtime contracts and E2E. Declare all external
inputs such as deployment files and image build contexts. Avoid directory-only
service discovery as the release selector.

## Publication boundary

container:build identifies a deployable unit; libraries have no image target.
container:push is side-effecting and never cacheable. container:build also defaults
to no cache: replaying a cached log cannot restore an image into the container daemon.

Publishing is not enabled in this foundation. Add Nx Release groups and registry
coordinates with actual deployable projects. Prefer independent versions unless
components intentionally share a lifecycle. Affected selection determines candidate
work; Nx Release and release history determine version/release decisions.
Publication selection must compare with the last successful publication, not just
the latest successful CI build, preserving unshipped changes across deferred or
failed releases. Coordinate image versions and immutable manifest references.

PR jobs use read-only permissions without publishing credentials. Release jobs must
validate the intended trusted revision and use the authorized GitHub App workflow
for signed repository changes. Do not copy bespoke legacy release scripts without
a concrete requirement.

## Security

Security CI runs on PRs, main and weekly. It audits all NuGet dependencies including
transitives, Bun's lockfile and Git history with checksum-pinned Gitleaks. Dependency
findings at any severity and secret findings fail their job. Redacted evidence is
uploaded. CodeQL scans C#, JavaScript/TypeScript and Actions with the extended
security suite and uploads findings. C# is built under tracing, including generated
code. There is no personal token or publication credential in these jobs.

Require Affected checks, Security gate and Workflow quality gate in repository rules.
Teck.Cloud is public. Enable code scanning and add a code-scanning ruleset to block
the selected alert severities:
a successful CodeQL analysis/upload alone does not mean zero findings. These server
settings have not been changed locally. Workflows need a pushed commit to run on
GitHub; local validation cannot claim a GitHub Actions pass.

Nx 24.0.6 pins vulnerable minimatch 10.2.5. A temporary Bun override uses 11.0.8
for GHSA-g3hv-75wg-m249. Bun patchedDependencies applies patches/nx@24.0.6.patch
to replace three removed makeRe calls with Minimatch.match. Keep the override and
patch together until aligned stable Nx packages natively support the patched
dependency. The Nx glob regression tests cover project selection, cached file
matching, generator tree globs and release branch matching; retain them through
upgrades. Frozen Bun installs must apply the recorded patch.
Image vulnerability scans, signing and SBOM publication belong with the first
real deployable image/release pipeline; no empty publishing jobs are installed.

## Additional gates and provenance

Workflow quality is a separate required check. It validates Conventional Commit PR
squash titles (including on title edits), workflow syntax/shell scripts with
Actionlint, and workflow security with Zizmor. Inputs such as PR titles are passed
as environment data, never interpolated as shell code. External actions are pinned
to full commit SHAs. Actionlint and Gitleaks downloads are checksum-verified.

Security gate also requires Trivy configuration scanning (high/critical findings)
and, on PRs, Dependency Review (low and above). A dependency-review failure cannot
be hidden as an intentional skip on a PR. Generated build output is excluded from
configuration scanning; source manifests are included. CodeQL alert blocking still
requires the GitHub code-scanning ruleset described above.

Dependabot checks Bun, central NuGet packages and Actions weekly. Nx updates stay
together. Version updates wait seven days; security updates do not. Updates still
need all applicable checks and review. OpenSSF Scorecard runs weekly, on main and
manually, retaining an advisory Actions artifact rather than publishing to the Scorecard service or
blocking on an arbitrary score.

The **Foundation candidate** workflow builds actual shared-library packages using
an isolated reusable builder, then attests and verifies the archive. See
[supply-chain.md](supply-chain.md) for the SLSA Build L3 target, verification policy,
public-repository availability and outstanding GitHub activation evidence. This is
not application-image publication and does not claim achieved L3 before verification.

[ci-roadmap.md](ci-roadmap.md) records everything deliberately deferred, the trigger
for implementing it and the evidence needed to accept it.

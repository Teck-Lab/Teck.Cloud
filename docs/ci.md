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
unit/integration set once on source changes to enforce the [coverage ratchet](coverage.md).
Changes confined to Markdown, docs, deployment and GitHub configuration leave the
coverage job green without running its build or tests. There is
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

GitHub release metadata is enabled for the repository. Release Drafter uses
Conventional Commit PR titles to group changes and calculate one repository version.
It includes documentation and deployment changes as patch releases. Once CI and
Security and quality pass for the exact current main commit, the Release
candidate workflow updates the stable draft and publishes an RC for that commit.
Each completed check triggers one evaluation; runs with unfinished or failed
checks exit without publishing. The last successful check can publish the RC.
If main advances before verification finishes, the superseded commit does not
receive a separate RC. Re-running CI for the same commit does not intentionally
publish another RC.
The Publish stable release workflow is manually dispatched from main, checks those
same workflows for the exact current main commit, requires a matching stable draft,
and publishes it. Backstage can dispatch this workflow later without changing the
release logic. These GitHub releases contain notes and tags, not application images,
NuGet packages or attested binaries.

Application publication remains deferred until actual deployable projects and
registry coordinates exist. Add Nx Release groups only if independent application
versioning is needed; do not introduce a second version calculator for the
repository release. Publication selection must compare with the last successful
publication, not just the latest successful CI build, preserving unshipped changes
across deferred or failed releases. Coordinate image versions and immutable
manifest references.

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

Require Affected checks and Security and quality gate in repository rules. Each
workflow publishes its own result directly, without a polling job. A PR title
edit reruns the combined Security and quality workflow so its gate reflects the
new title. CI keeps its result for the unchanged head commit.

Teck.Cloud is public. Enable code scanning and add a code-scanning ruleset to block
the selected alert severities:
a successful CodeQL analysis/upload alone does not mean zero findings. These server
settings have not been changed locally. Workflows need a pushed commit to run on
GitHub; local validation cannot claim a GitHub Actions pass.

The aligned Nx 24.1 beta uses minimatch 11 without a local package patch. The Bun
override keeps transitive minimatch instances on 11.0.8 for GHSA-g3hv-75wg-m249.
The Nx glob regression tests cover project selection, cached file matching,
generator tree globs and release branch matching; retain them through upgrades.
Return to aligned stable Nx packages when their minimatch integration is available.
The affected image lane scans the built Keycloak candidate for high/critical OS
and library vulnerabilities with Trivy. It retains a full JSON report and blocks
on findings with an available fix. The refreshed Keycloak 26.8.2 base and a
checksum-verified Guava JAR update resolved the current fixable findings. Five
upstream high-severity findings without a published fix remain in the full report;
reassess them on each Keycloak update. Other container targets are test-only;
add image scans when they become deployable. This validates the local build,
not a future published registry digest. Image signing and SBOM publication remain
deferred until there is an artifact publication pipeline.

## Additional gates and provenance

The Security and quality workflow validates Conventional Commit PR squash titles
(including on title edits), workflow syntax/shell scripts with Actionlint, and
workflow security with Zizmor. Inputs such as PR titles are passed as
environment data, never interpolated as shell code. External actions are pinned
to full commit SHAs. Actionlint and Gitleaks downloads are checksum-verified.

The Security and quality gate also requires Trivy configuration scanning
(high/critical findings), rendered local identity manifest checks and, on PRs,
Dependency Review (low and above). Kubeconform validates built-in resources
strictly. The operator and Gateway API CRDs are explicitly skipped by this
offline schema check; the KIND E2E lane applies those real resources against
installed CRDs. Kubescape scans the rendered local identity resources against
the NSA framework and blocks high/critical posture findings. The current local
baseline has three medium findings for non-root checks and network isolation;
they remain visible in the JSON artifact. Kyverno tests the workload policy with good/bad fixtures and
applies it to the same rendered overlay. The ambient manifest is appended for
full-mode coverage; sandbox mode does not install mesh CRDs. These local checks
do not establish a production overlay security posture.
A dependency-review failure cannot
be hidden as an intentional skip on a PR. Generated build output is excluded from
configuration scanning; source manifests are included. The deliberately insecure
Kyverno negative fixture is excluded from Trivy configuration scanning and is
required to fail in the Kyverno policy tests. CodeQL alert blocking still
requires the GitHub code-scanning ruleset described above.

Dependabot checks Bun, central NuGet packages and Actions weekly. Nx updates stay
together. Version updates wait seven days; security updates do not. Updates still
need all applicable checks and review. OpenSSF Scorecard runs as an advisory job
in Security and quality on main, weekly and manual runs. It retains an Actions
artifact without publishing to the Scorecard service or blocking on an arbitrary score.

Artifact builds and attestations are deferred until there is a package or image
publication consumer. See [supply-chain.md](supply-chain.md) for the future
provenance requirements; the current releases contain metadata only.

[ci-roadmap.md](ci-roadmap.md) records everything deliberately deferred, the trigger
for implementing it and the evidence needed to accept it.

# Foundation validation - 2026-11-14

This records local evidence for the prepared Teck.Cloud foundation. It replaces the
2026-11-12 empty-workspace snapshot. It is not evidence of a GitHub Actions run,
a deployed business application or achieved SLSA Build Level 3.

## Reproducible installation and build

- Release solution build passed with zero warnings and zero errors on .NET SDK
  10.0.401. Local validation used Windows, Node 24.18.0 and the repository-pinned
  Bun 1.4.1. The .NET SDK policy permits patches in its selected feature band.
- A fresh isolated frozen Bun install reapplied the version-pinned Nx compatibility
  patch without changing bun.lock. Actual wildcard project selection and regression
  tests covered exclusions, tags/directories, braces/extglobs, generator changes,
  release-branch patterns and invalid patterns. The secure minimatch pin was retained.
- All 13 NuGet lockfiles restored in locked mode on Windows and Linux with SDK
  10.0.401. The Linux restore left their bytes unchanged. CI enforces locked mode,
  including security and candidate package builds.
- The analyzer-policy verifier passed 17 checks under GITHUB_ACTIONS=true, including
  rejection of dependency drift with NU1004. Only its disposable synthetic fixtures
  establish their own lockfiles; repository projects do not bypass locked restore.
- Three SharedKernel candidate packages built with --no-restore. Their package
  versions and internal SharedKernel dependency versions matched the supplied
  candidate version. These local bytes have not been attested or published.

## Behavioral verification

The suites exercise implemented foundation boundaries, with Shouldly assertions,
NSubstitute at interface boundaries and locally seeded generated data where useful.
The final checks passed 105 .NET tests (99 unit/integration, two architecture,
two Aspire E2E and one KIND test in each mode) plus 13 repository-tooling tests.

- SharedKernel unit and architecture suites verify contracts, boundaries, identity
  propagation, result mapping and dependency direction.
- Real Testcontainers integration suites verify PostgreSQL persistence and tenant
  isolation, specifications, Redis cache behavior, RabbitMQ/Wolverine durability,
  health checks and correlated/redacted telemetry. Aspire does not replace them.
- All 23 AppHost unit cases passed, including 16 startup-preflight cases. A real
  direct-executable negative check rejected a reachable Podman API whose machine
  SSH path was broken, before resources were created.
- Both Aspire E2E tests passed: actual SQL/broker/OIDC discovery and Redis Sentinel
  primary-loss recovery through existing consumers.
- Both full and sandbox KIND tests passed in isolated WSL2/Podman clusters. Each
  asserts the components of the requested mode before starting resources. Full
  verifies Cilium/ztunnel rollout and ambient pod redirection; sandbox verifies
  kindnet and absence of the Istio namespace. Both verify pod readiness, DNS, Job
  completion and deletion of their owned cluster.
  The expanded common-platform tests also passed unattended in both modes:
  sandbox in 3m53s and full in 4m35s. They verified CA-validated HTTPS through
  kgateway to an Argo Rollout, CNPG persistence across separate SQL connections,
  authenticated Redis writes/reads through a Sentinel-discovered primary, and
  OpenBao automatic initialization and unsealing. The Sentinel probe waits for
  primary discovery; Pod readiness alone is insufficient. OpenBao API egress is
  resolved from the cluster EndpointSlice, with Cilium mode in the full profile.
- Thirteen repository-tooling tests passed, including the aggregate CI gate's
  rejection of failed, cancelled, skipped, missing and unexpected validation lanes.

The original Windows Podman machine SSH endpoint was unavailable. Successful
Aspire E2E used a temporary test-only machine profile and SSH forward to the
existing WSL2 machine. Global runtime settings and the user's Docker Sandbox were
not changed. This does not claim the machine's normal connection was repaired.
One initial full-mode run was interrupted by a concurrent Aspire CLI diagnostic;
the isolated rerun passed. Diagnostics subsequently used direct executables.

Coverage uses the full unit/integration scope and the unchanged ratchet:

| Assembly                    | Line % | Branch % |
| --------------------------- | ------ | -------- |
| SharedKernel.Core           | 60.759 | 65.384   |
| SharedKernel.Infrastructure | 89.514 | 72.348   |
| SharedKernel.Persistence    | 85.043 | 80       |

Coverage excludes AppHost orchestration as documented in [coverage](coverage.md).
The initial baseline is a non-regression floor, not a claim of comprehensive coverage.

## Repository and workflow checks

Biome, Nx reference synchronization, Actionlint and Zizmor passed. Scoped Markdown
links were checked. Deployment environments are base, local, development, staging
and production. All deployment guidance uses the production name.

CI now resolves one affected baseline, then runs fast checks, complete coverage,
Aspire E2E, full/sandbox KIND and image builds in parallel jobs. The stable
Affected checks gate requires every lane to succeed. A legitimate empty affected
selection is supported; a skipped or failed job does not satisfy the gate.

Reports remain under .artifacts and are deliberately excluded from source control.
Relevant local records include precommit-build.log, precommit-tooling.log,
precommit-coverage.log, precommit-aspire-final.log, precommit-kind-full-final.log,
precommit-kind-sandbox.log, startup-negative-direct.log, dependency-policy.log and
dependency-linux-restore.log. The expanded platform runs are recorded in
platform-sandbox-verified.log and platform-full-verified.log; the analyzer-policy
rerun is in platform-policy.log. The Release solution build and 23 AppHost tests
were rerun after the platform changes. GitHub workflows upload their own
run-specific evidence.

## Gateway API experimental and OpenBao update

The platform suite was rerun with Gateway API 1.7.0 experimental CRDs and OpenBao
2.7.2. Both disposable profiles passed: sandbox in 3m45s and full in 5m03s. Live
Gateway CRD annotations confirmed the experimental channel in both clusters. The
existing HTTPS, CNPG, Sentinel and initialized/unsealed OpenBao assertions passed;
this does not claim coverage of every experimental route type or field. Both owned
clusters were removed. Logs: platform-sandbox-experimental-bao272.log and
platform-full-experimental-bao272.log under .artifacts.

## Remaining external and application evidence

GitHub-hosted CI, repository protections, CodeQL results, Scorecard results and
OIDC provenance verification still require the committed branch and actual runs.
Follow [the activation checklist](supply-chain.md#activation-and-acceptance-evidence);
local workflow validation does not satisfy those checks.

No business service, gateway, frontend or mobile application exists yet. There is
no complete browser journey, production mTLS/default-deny acceptance, application
image release or deployment-overlay acceptance claim. The KIND fixtures establish
platform behavior, not business acceptance, production OpenBao security, database
backup/restore, Sentinel failover in Kubernetes, or Gateway API weighted-canary
progression. Argo CD and Kargo are not installed. Add those proofs with their real
consumers, following
[the CI roadmap](ci-roadmap.md) and [testing layers](testing.md).

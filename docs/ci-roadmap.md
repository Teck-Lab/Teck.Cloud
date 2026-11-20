# CI and security follow-up backlog

The new repository adopts useful controls from Teck.Monorepo without copying its
service names, publication credentials or placeholder checks. This list separates
working local implementation from GitHub activation and application-dependent work.

## Implemented now

| Control                      | Implementation                                                                                                       |
| ---------------------------- | -------------------------------------------------------------------------------------------------------------------- |
| Affected CI                  | Nx transitive graph; build/lint/typecheck/unit; full unit/integration coverage ratchet; affected Aspire and KIND E2E |
| Repository release notes     | Release Drafter stable draft, CI-gated RCs and manual stable GitHub release; no application artifact publication     |
| Dependency and secret audits | All-severity NuGet/Bun audit and redacted full-history Gitleaks                                                      |
| Code scanning                | CodeQL C#, JS/TS and Actions with extended queries                                                                   |
| PR dependency delta          | Dependency Review blocks low-and-higher vulnerabilities; full lockfile audits remain necessary                       |
| Configuration scan           | Trivy blocks high/critical misconfigurations, retains SARIF; scans source, excludes generated outputs                |
| Workflow quality             | Checksum-pinned Actionlint, pinned Zizmor, Conventional Commit squash-title validation with tests                    |
| Updates                      | Weekly Dependabot for Bun, NuGet and Actions; aligned Nx group; seven-day version cooldown; no automatic merge       |
| Repository posture           | Weekly/main/manual OpenSSF Scorecard JSON, advisory, no publication to the Scorecard service                         |

## Activation before relying on the gates

- [ ] Complete the repository protections, feature configuration and successful-run evidence
      in [supply-chain.md](supply-chain.md). Local syntax/scanner/build success does not
      prove GitHub token permissions, feature configuration or branch protection.
- [ ] Review the initial Scorecard report, record justified findings and fix actionable
      ones. Do not add a numeric pass threshold until its checks are applicable to this
      public repository. Keep it outside the blocking PR security gate.
- [ ] Review the first Dependabot PRs for all three ecosystems. Verify central NuGet
      props and Nx grouping update together and all gates actually run. Security updates
      are not delayed by the version-update cooldown.

## Deferred until there is a real consumer

| Work                              | Trigger and acceptance criteria                                                                                                                                                                                 |
| --------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Application image build/release   | First deployable service or gateway. Add Nx targets and independent release groups as appropriate; select from last successful publication, not last CI. Prove an earlier unshipped change is not skipped.      |
| Package/image SBOM and provenance | First artifact publication. Generate SPDX/CycloneDX for the exact artifact digest, scan it, attest from an isolated builder and verify before promotion. Prove the published bytes match the verified artifact. |
| Permanent artifact distribution   | First feed/registry/release publication. Retain verified bytes, provenance and verification policy beyond Actions retention. Prove download/verification outside the producing run.                             |
| Scheduled image rescanning        | Once images are actually published. Rescan immutable supported release digests and report newly disclosed vulnerabilities without rebuilding or silently changing tags.                                         |
| VEX                               | Real vulnerability triage. Require evidence and ownership for affected/not-affected decisions; do not automatically claim not-affected from scanner absence.                                                    |
| Semgrep service rules             | First application slices. Select maintained rules that add value beyond CodeQL/analyzers and test both bad/good fixtures. The old repo's legacy scanner pin and nonblocking invocation are not copied.          |
| OpenAPI lint/breaking changes     | First gateway schema. Generate and validate the actual document, compare with the released contract, and prove breaking changes fail. The old placeholder echo job is not a validation implementation.          |
| Preview DAST and API fuzzing      | First reachable application preview. Add ZAP, Schemathesis and security-header checks against an allowlisted disposable target; prove authenticated flows and avoid production mutation.                        |
| Browser E2E                       | First web feature. Run a real browser against the application assembled by Aspire, covering gateway/backend/database and visible behavior. Keep integration tests on Testcontainers.                            |
| Frontend coverage                 | First TS application/package tests. Add the selected runner and enforce meaningful coverage alongside .NET; no empty suite or arbitrary inherited threshold.                                                    |
| Mobile tests                      | After the user selects a strategy. Preserve React Native workspace support; do not choose a runner prematurely.                                                                                                 |
| Dependency auto-merge             | After stable branch protection/gates. Limit eligibility, use authorized App automation, prove failed checks block merging, and retain human review for major/security-sensitive changes.                        |
| Merge queue                       | If enabled on GitHub, add `merge_group` triggers and verify all required checks on merge-group commits before enabling the queue.                                                                               |

Paperclip issue automation is not a build/security gate and is not migrated without
an actual Teck.Cloud workflow requiring it. Terraform and production cluster changes
are outside this foundation work.

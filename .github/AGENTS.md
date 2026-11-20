# GitHub automation

Read [CI](../docs/ci.md) before workflow changes. For artifact/release work read
[supply chain](../docs/supply-chain.md); deferred work is in [CI roadmap](../docs/ci-roadmap.md).

- Keep PR code on read-only permissions without publication credentials. Pin
  external actions to full SHAs, verify binary downloads and pass untrusted values
  as data rather than shell interpolation.
- Preserve Nx transitive affected selection and the full coverage ratchet.
  Publication selection uses last successful publication, not merely last CI.
- Keep Security and quality gate strict: every required job must succeed; only
  the explicit non-PR dependency-review skip is allowed. CodeQL uploads are not
  zero-alert proof.
- Keep Scorecard advisory in the combined workflow. Dependabot updates must
  respect aligned Nx/package groups and still pass checks/review.
- When an artifact builder is added, keep build code separate from signing and
  verification permissions. Pin source and builder identities and reject tampering.
- Use ephemeral workflow credentials, never developer PATs. Do not claim SLSA L3,
  branch protection or successful GitHub execution from local YAML validation.
- Validate with Actionlint, Zizmor and applicable tooling tests. Explain narrow
  exceptions; never suppress a security rule broadly just to obtain a pass.

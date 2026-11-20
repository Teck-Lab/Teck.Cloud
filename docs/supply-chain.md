# Supply-chain security and SLSA Build L3

## Scope and current status

The target is **SLSA Build Level 3 for produced artifacts**, not a blanket repository
certification. The implemented path builds the three existing SharedKernel NuGet
packages into `teck-cloud-foundation.tar.gz`. These are internal CI candidates;
there is no NuGet feed publication or application image release yet.

The workflow implements GitHub's documented reusable-build/attestation approach.
Local package builds and workflow checks are verified. **No GitHub-hosted run or
OIDC attestation has been verified yet. Do not claim achieved L3 until the activation
and evidence checklist below is complete.**

## Build and trust boundary

Run **Foundation candidate** from the GitHub Actions UI on `main`, after CI and
Security and quality have succeeded for the exact source SHA. A
missing, failed or still-running check blocks the candidate. The caller has no
inputs that can select another artifact, source ref, command or digest.

`reusable-foundation-build.yml` owns three fresh GitHub-hosted jobs:

1. Build checks the exact-commit workflow results, checks out the caller commit and
   packs Core, Persistence and Infrastructure with one run-specific prerelease
   version. No restored build cache, signing permission or persistent credential
   is available to build code. Source SHA is also recorded in the archive.
2. Attest downloads only this run attempt's immutable build artifact. It never
   checks out source, extracts the tarball or executes its contents. GitHub's
   official action computes the digest from the downloaded bytes and generates
   signed SLSA provenance using short-lived OIDC. Only this job can attest.
3. Verify downloads the artifact and Sigstore bundle without executing them. It
   checks repository, reusable workflow, workflow commit, source commit, main ref
   and GitHub-hosted runner identity. Negative checks must reject changed bytes,
   an incorrect source SHA and a different builder workflow.

The caller cannot replace build instructions through workflow inputs. Review and
protect the reusable workflow and its callers: an approved malicious change to the
builder itself is outside what a valid signature can detect. SLSA is provenance
and build integrity, not proof of vulnerability-free software or reproducibility.

## Artifacts and consumers

`foundation-verified-<attempt>` contains the archive, `provenance.sigstore.json`
and `verification.json`, retained for 90 days. The intermediate build artifact
expires after seven days. Use only the verified output of a successful full run.
The attested intermediate output is not evidence that verification passed.

Consumers must verify again before use, using an independently selected trusted
source SHA and builder revision, rather than trusting values supplied by an artifact:

```sh
gh attestation verify teck-cloud-foundation.tar.gz \
  --repo Teck-Lab/Teck.Cloud \
  --signer-workflow Teck-Lab/Teck.Cloud/.github/workflows/reusable-foundation-build.yml \
  --signer-digest "$TRUSTED_SHA" \
  --source-digest "$TRUSTED_SHA" --source-ref refs/heads/main \
  --deny-self-hosted-runners --bundle provenance.sigstore.json
```

Here the reusable builder is intentionally at the same commit as the source.
A future shared builder repository must pin its immutable revision separately.
CI uses only the ephemeral GitHub workflow token, never a developer PAT. These
commands do not authorize agents to publish through personal GitHub credentials.

## Activation and acceptance evidence

These are repository/organization controls or GitHub-run evidence, not local
files that can be declared complete by writing YAML:

- [x] Confirm repository visibility: GitHub reports `Teck-Lab/Teck.Cloud` as public
      (verified anonymously on 2026-11-14). Artifact attestations are available for
      public repositories on current GitHub plans; Enterprise Cloud is not required.
      Public repository attestations use the public Sigstore transparency service.
- [ ] Enable Actions, dependency graph, CodeQL and Dependency Review. Confirm
      Scorecard can read this repository with its workflow token. Do not introduce
      a broad PAT to make a check pass.
- [ ] Protect main and require PR review, **Affected checks** and
      **Security and quality gate**. Configure Code Scanning rules to block relevant alert
      severities; successful analysis alone is not zero findings. Apply changes to
      workflow/build/security policy through review; restrict bypass permissions.
- [ ] Assign actual code owners for `.github/`, build props, dependency policy and
      `tools/`. No invented team or unusable CODEOWNERS entry is installed.
- [ ] Require SHA-pinned external actions, least-privilege tokens and GitHub-hosted
      jobs for this builder. Review action/tool upgrades; do not add secrets or source
      execution to the attestation job.
- [ ] Push the prepared change through normal review, obtain successful checks,
      then run Foundation candidate. Preserve the exact source/builder SHA, run URL,
      verification JSON and artifact digest as evidence.
- [ ] Confirm all three negative verification checks fail as intended, and perform
      an independent download and verification. Confirm a non-main candidate does not
      build or sign and missing/failed checks block the build.
- [ ] Review the trust boundary against SLSA build requirements before making an
      L3 claim for this artifact. GitHub's guide is not an external certification.

There is no permanent release distribution yet. Promotion must retain the exact
verified bytes and provenance beyond Actions retention; rebuilding after verification
would create a different artifact requiring new provenance.

## References

- [GitHub's SLSA Build L3 reusable workflow guide](https://docs.github.com/en/actions/how-tos/secure-your-work/use-artifact-attestations/increase-security-rating)
- [Artifact attestation availability](https://docs.github.com/en/actions/how-tos/secure-your-work/use-artifact-attestations/use-artifact-attestations)
- [Verification policy flags](https://cli.github.com/manual/gh_attestation_verify)
- [SLSA build requirements](https://slsa.dev/spec/v1.2/build-requirements)

The older `slsa-framework/slsa-github-generator` project now describes itself as no
longer actively maintained and recommends GitHub artifact attestations. We do not
introduce it as a new dependency. See its [upstream notice](https://github.com/slsa-framework/slsa-github-generator).

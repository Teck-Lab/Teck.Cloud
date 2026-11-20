# Supply-chain and artifact provenance

## Current scope

The repository currently publishes GitHub release notes, prerelease tags and a
stable draft. It does not publish application images or NuGet packages. The unused
Foundation candidate and Reusable foundation build workflows were removed; no
artifact attestation or SLSA Build Level 3 claim is active.

Security and quality runs CodeQL for C#, JavaScript/TypeScript and Actions. GitHub's
managed CodeQL default setup is not configured. A successful CodeQL upload does
not prove there are no alerts; configure code-scanning rules for the alert
severities the repository intends to block.

## Future artifact boundary

Add artifact builds when there is a real feed, registry or deployment consumer.
The builder must select the exact reviewed source and produce immutable bytes.
Keep build code without signing permissions. Attestation jobs should neither
check out source nor execute the artifact. Verification must pin repository,
source commit, builder workflow and runner identity, and reject changed bytes and
incorrect identities. Preserve the verified artifact and provenance beyond Actions
retention so consumers can verify what they receive.

SLSA Build Level 3 is a future target for produced artifacts, not a repository
certification. Do not claim it until GitHub-hosted attestation and independent
verification succeed on the actual published artifact.

## Activation and acceptance evidence

- Protect `main` with review and the **Affected checks** and **Security and quality gate**
  status checks. Add code-scanning rules for selected alert severities.
- Assign real code owners for workflow, build and dependency policy changes.
- For the first published artifact, verify the exact bytes, source and builder
  identities outside the producing workflow. Test that changed bytes and wrong
  identities are rejected.
- Retain the run URL, artifact digest, attestation and verification output as
  evidence. A local workflow syntax check is not runtime attestation evidence.

See [CI](ci.md) and the [deferred work](ci-roadmap.md) for the current release
boundary and the trigger for artifact publication.

## References

- [GitHub artifact attestations](https://docs.github.com/en/actions/how-tos/secure-your-work/use-artifact-attestations/use-artifact-attestations)
- [SLSA build requirements](https://slsa.dev/spec/v1.2/build-requirements)

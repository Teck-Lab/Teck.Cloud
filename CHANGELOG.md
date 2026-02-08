# v0.8.1 (Sun Feb 08 2026)

#### 🐛 Bug Fix

- fix: update attest-sbom SHA to valid commit ([@CaptainPowerTurtle](https://github.com/CaptainPowerTurtle))
- fix: update codeql-action SHA to valid commit ([@CaptainPowerTurtle](https://github.com/CaptainPowerTurtle))

#### Authors: 1

- CptPowerTurtle ([@CaptainPowerTurtle](https://github.com/CaptainPowerTurtle))

---

# v0.7.0 (Sun Feb 08 2026)

#### 🚀 Enhancement

- chore(deps)(deps): Bump actions/attest-build-provenance from 3.1.0 to 3.2.0 [#162](https://github.com/Teck-Lab/Teck.Cloud/pull/162) ([@dependabot[bot]](https://github.com/dependabot[bot]))
- chore(deps)(deps): Bump danielpalme/ReportGenerator-GitHub-Action from 5.2.4 to 5.5.1 [#158](https://github.com/Teck-Lab/Teck.Cloud/pull/158) ([@dependabot[bot]](https://github.com/dependabot[bot]))
- chore(deps)(deps): Bump aquasecurity/trivy-action from 0.25.0 to 0.33.1 [#161](https://github.com/Teck-Lab/Teck.Cloud/pull/161) ([@dependabot[bot]](https://github.com/dependabot[bot]))
- chore(deps)(deps): Bump docker/login-action from 3.3.0 to 3.7.0 [#159](https://github.com/Teck-Lab/Teck.Cloud/pull/159) ([@dependabot[bot]](https://github.com/dependabot[bot]))

#### 🐛 Bug Fix

- fix: correct action SHAs for supply chain security ([@CaptainPowerTurtle](https://github.com/CaptainPowerTurtle))
- fix: Update SHA for create-github-app-token action ([@CaptainPowerTurtle](https://github.com/CaptainPowerTurtle))

#### ⚠️ Pushed to `main`

- Merge branch 'main' of https://github.com/Teck-Lab/Teck.Cloud ([@CaptainPowerTurtle](https://github.com/CaptainPowerTurtle))
- infra. updated workflow to be more secure ([@CaptainPowerTurtle](https://github.com/CaptainPowerTurtle))

#### Authors: 2

- [@dependabot[bot]](https://github.com/dependabot[bot])
- CptPowerTurtle ([@CaptainPowerTurtle](https://github.com/CaptainPowerTurtle))

---

# v0.6.0 (Sun Feb 08 2026)

#### 🚀 Enhancement

- feat: Remove redundant Docker Buildx setup steps in docker-publish.yaml ([@CaptainPowerTurtle](https://github.com/CaptainPowerTurtle))

#### 🐛 Bug Fix

- fix: Use HARBOR_USERNAME and HARBOR_PASSWORD secrets for SBOM attestation ([@CaptainPowerTurtle](https://github.com/CaptainPowerTurtle))

#### ⚠️ Pushed to `main`

- Merge branch 'main' of https://github.com/Teck-Lab/Teck.Cloud ([@CaptainPowerTurtle](https://github.com/CaptainPowerTurtle))

#### Authors: 1

- CptPowerTurtle ([@CaptainPowerTurtle](https://github.com/CaptainPowerTurtle))

---

# v0.5.2 (Sun Feb 08 2026)

#### 🐛 Bug Fix

- fix: Correct GH_TOKEN environment variable in release workflow ([@CaptainPowerTurtle](https://github.com/CaptainPowerTurtle))

#### ⚠️ Pushed to `main`

- Merge branch 'main' of https://github.com/Teck-Lab/Teck.Cloud ([@CaptainPowerTurtle](https://github.com/CaptainPowerTurtle))

#### Authors: 1

- CptPowerTurtle ([@CaptainPowerTurtle](https://github.com/CaptainPowerTurtle))

---

# v0.5.1 (Sun Feb 08 2026)

#### 🐛 Bug Fix

- fix: Ensure release workflow has token with write permissions ([@CaptainPowerTurtle](https://github.com/CaptainPowerTurtle))

#### Authors: 1

- CptPowerTurtle ([@CaptainPowerTurtle](https://github.com/CaptainPowerTurtle))

---

# v0.5.0 (Sat Feb 07 2026)

#### 🚀 Enhancement

- feat: Migrate docker publishing to Harbor ([@CaptainPowerTurtle](https://github.com/CaptainPowerTurtle))

#### Authors: 1

- CptPowerTurtle ([@CaptainPowerTurtle](https://github.com/CaptainPowerTurtle))

---

# v0.4.0 (Sat Feb 07 2026)

#### 🚀 Enhancement

- feat(docker): add tags to docker publish workflows ([@CaptainPowerTurtle](https://github.com/CaptainPowerTurtle))

#### ⚠️ Pushed to `main`

- Merge branch 'main' of https://github.com/Teck-Lab/Teck.Cloud ([@CaptainPowerTurtle](https://github.com/CaptainPowerTurtle))

#### Authors: 1

- CptPowerTurtle ([@CaptainPowerTurtle](https://github.com/CaptainPowerTurtle))

---

# v0.3.0 (Sat Feb 07 2026)

#### 🚀 Enhancement

- feat(docker): publish images to Harbor registry ([@CaptainPowerTurtle](https://github.com/CaptainPowerTurtle))
- feat(docker): add Harbor registry login to publish workflows ([@CaptainPowerTurtle](https://github.com/CaptainPowerTurtle))

#### Authors: 1

- CptPowerTurtle ([@CaptainPowerTurtle](https://github.com/CaptainPowerTurtle))

---

# v0.2.2 (Fri Feb 06 2026)

#### 🐛 Bug Fix

- fix: Correct working directory for manifest creation ([@CaptainPowerTurtle](https://github.com/CaptainPowerTurtle))

#### Authors: 1

- CptPowerTurtle ([@CaptainPowerTurtle](https://github.com/CaptainPowerTurtle))

---

# v0.2.1 (Fri Feb 06 2026)

#### 🐛 Bug Fix

- fix(docker): Correct working directory and digest extraction for auth images ([@CaptainPowerTurtle](https://github.com/CaptainPowerTurtle))

#### ⚠️ Pushed to `main`

- Merge branch 'main' of https://github.com/Teck-Lab/Teck.Cloud ([@CaptainPowerTurtle](https://github.com/CaptainPowerTurtle))

#### Authors: 1

- CptPowerTurtle ([@CaptainPowerTurtle](https://github.com/CaptainPowerTurtle))

---

# v0.2.0 (Fri Feb 06 2026)

#### 🚀 Enhancement

- feat(config): add git-tag plugin ([@CaptainPowerTurtle](https://github.com/CaptainPowerTurtle))
- feat(release): add debug commands to release workflow ([@CaptainPowerTurtle](https://github.com/CaptainPowerTurtle))
- feat: build and publish auth service docker image ([@CaptainPowerTurtle](https://github.com/CaptainPowerTurtle))
- feat: enable upload assets for changelog ([@CaptainPowerTurtle](https://github.com/CaptainPowerTurtle))
- feat: add canary release workflow ([@CaptainPowerTurtle](https://github.com/CaptainPowerTurtle))

#### 🐛 Bug Fix

- fix: trigger docker publish on canary releases and remove unused branches ([@CaptainPowerTurtle](https://github.com/CaptainPowerTurtle))
- chore: Bump TngTech.ArchUnitNET and TngTech.ArchUnitNET.xUnitV3 [#106](https://github.com/Teck-Lab/Teck.Cloud/pull/106) ([@dependabot[bot]](https://github.com/dependabot[bot]))

#### ⚠️ Pushed to `main`

- Remove deprecated env var and auto shipit improvements in release-services.yaml ([@CaptainPowerTurtle](https://github.com/CaptainPowerTurtle))
- Refactor docker-publish.yaml to trigger on releases and remove auth-specific logic ([@CaptainPowerTurtle](https://github.com/CaptainPowerTurtle))
- Introduce dedicated workflow for building and publishing auth images ([@CaptainPowerTurtle](https://github.com/CaptainPowerTurtle))
- Add Dockerfile and README for Keycloak authentication service ([@CaptainPowerTurtle](https://github.com/CaptainPowerTurtle))
- refactor: simplify labels in .autorc ([@CaptainPowerTurtle](https://github.com/CaptainPowerTurtle))

#### Authors: 2

- [@dependabot[bot]](https://github.com/dependabot[bot])
- CptPowerTurtle ([@CaptainPowerTurtle](https://github.com/CaptainPowerTurtle))

---

# v0.1.13 (Sun Jan 25 2026)

#### ⚠️ Pushed to `main`

- Merge branch 'main' of https://github.com/Teck-Lab/Teck.Cloud ([@CaptainPowerTurtle](https://github.com/CaptainPowerTurtle))
- Update docker-publish.yaml ([@CaptainPowerTurtle](https://github.com/CaptainPowerTurtle))

#### Authors: 1

- CptPowerTurtle ([@CaptainPowerTurtle](https://github.com/CaptainPowerTurtle))

---

# v0.1.12 (Sun Jan 25 2026)

#### ⚠️ Pushed to `main`

- Merge branch 'main' of https://github.com/Teck-Lab/Teck.Cloud ([@CaptainPowerTurtle](https://github.com/CaptainPowerTurtle))
- Update docker-publish.yaml ([@CaptainPowerTurtle](https://github.com/CaptainPowerTurtle))

#### Authors: 1

- CptPowerTurtle ([@CaptainPowerTurtle](https://github.com/CaptainPowerTurtle))

---

# v0.1.11 (Sun Jan 25 2026)

#### ⚠️ Pushed to `main`

- Update release-services.yaml ([@CaptainPowerTurtle](https://github.com/CaptainPowerTurtle))

#### Authors: 1

- CptPowerTurtle ([@CaptainPowerTurtle](https://github.com/CaptainPowerTurtle))

---

# v0.1.10 (Sun Jan 25 2026)

#### ⚠️ Pushed to `main`

- Merge branch 'main' of https://github.com/Teck-Lab/Teck.Cloud ([@CaptainPowerTurtle](https://github.com/CaptainPowerTurtle))
- Update docker-publish.yaml ([@CaptainPowerTurtle](https://github.com/CaptainPowerTurtle))

#### Authors: 1

- CptPowerTurtle ([@CaptainPowerTurtle](https://github.com/CaptainPowerTurtle))

---

# v0.1.9 (Sun Jan 25 2026)

#### ⚠️ Pushed to `main`

- Update docker-publish.yaml ([@CaptainPowerTurtle](https://github.com/CaptainPowerTurtle))

#### Authors: 1

- CptPowerTurtle ([@CaptainPowerTurtle](https://github.com/CaptainPowerTurtle))

---

# v0.1.8 (Sun Jan 25 2026)

#### ⚠️ Pushed to `main`

- Merge branch 'main' of https://github.com/Teck-Lab/Teck.Cloud ([@CaptainPowerTurtle](https://github.com/CaptainPowerTurtle))
- Update docker-publish.yaml ([@CaptainPowerTurtle](https://github.com/CaptainPowerTurtle))

#### Authors: 1

- CptPowerTurtle ([@CaptainPowerTurtle](https://github.com/CaptainPowerTurtle))

---

# v0.1.7 (Sun Jan 25 2026)

#### ⚠️ Pushed to `main`

- Merge branch 'main' of https://github.com/Teck-Lab/Teck.Cloud ([@CaptainPowerTurtle](https://github.com/CaptainPowerTurtle))
- Update docker-publish.yaml ([@CaptainPowerTurtle](https://github.com/CaptainPowerTurtle))

#### Authors: 1

- CptPowerTurtle ([@CaptainPowerTurtle](https://github.com/CaptainPowerTurtle))

---

# v0.1.6 (Sun Jan 25 2026)

#### ⚠️ Pushed to `main`

- Merge branch 'main' of https://github.com/Teck-Lab/Teck.Cloud ([@CaptainPowerTurtle](https://github.com/CaptainPowerTurtle))
- Update docker-publish.yaml ([@CaptainPowerTurtle](https://github.com/CaptainPowerTurtle))

#### Authors: 1

- CptPowerTurtle ([@CaptainPowerTurtle](https://github.com/CaptainPowerTurtle))

---

# v0.1.5 (Sun Jan 25 2026)

#### ⚠️ Pushed to `main`

- Update docker-publish.yaml ([@CaptainPowerTurtle](https://github.com/CaptainPowerTurtle))

#### Authors: 1

- CptPowerTurtle ([@CaptainPowerTurtle](https://github.com/CaptainPowerTurtle))

---

# v0.1.4 (Sun Jan 25 2026)

#### ⚠️ Pushed to `main`

- Merge branch 'main' of https://github.com/Teck-Lab/Teck.Cloud ([@CaptainPowerTurtle](https://github.com/CaptainPowerTurtle))
- Update docker-publish.yaml ([@CaptainPowerTurtle](https://github.com/CaptainPowerTurtle))

#### Authors: 1

- CptPowerTurtle ([@CaptainPowerTurtle](https://github.com/CaptainPowerTurtle))

---

# v0.1.3 (Sun Jan 25 2026)

#### 🐛 Bug Fix

- fix: Update Dockerfile [#100](https://github.com/Teck-Lab/Teck.Cloud/pull/100) ([@CaptainPowerTurtle](https://github.com/CaptainPowerTurtle))

#### ⚠️ Pushed to `main`

- Update docker-publish.yaml ([@CaptainPowerTurtle](https://github.com/CaptainPowerTurtle))
- ci: workflow changes ([@CaptainPowerTurtle](https://github.com/CaptainPowerTurtle))
- chore: more workflow changes ([@CaptainPowerTurtle](https://github.com/CaptainPowerTurtle))
- chore: Add catalog service configs and update workflows ([@CaptainPowerTurtle](https://github.com/CaptainPowerTurtle))

#### Authors: 1

- CptPowerTurtle ([@CaptainPowerTurtle](https://github.com/CaptainPowerTurtle))

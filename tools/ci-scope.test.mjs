import assert from "node:assert/strict";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import test from "node:test";
import {
  getJobApplicability,
  isCoverageFull,
  readCiScope,
  scopeJobs,
  scopeSchemaVersion,
  scopeTargets,
  validateCiScope,
} from "./ci-scope.mjs";

function manifest() {
  return {
    schemaVersion: scopeSchemaVersion,
    event: "pull_request",
    head: "head-sha",
    validation: { base: "base-sha", baselineSource: "pull-request", fallbackReason: null },
    changedPaths: [{ status: "R100", oldPath: "docs/old.md", newPath: "docs/new.md" }],
    targets: Object.fromEntries(scopeTargets.map((target) => [target, []])),
    coverageFull: false,
    coverageComponents: [],
    jobs: Object.fromEntries(
      scopeJobs.map((job) => [
        job,
        { required: false, reason: "no applicable changes", expectedMatrix: [] },
      ]),
    ),
  };
}

test("manifest v1 accepts canonical empty scope and rejects unknown or missing fields", () => {
  assert.equal(validateCiScope(manifest()).schemaVersion, 1);
  assert.throws(
    () => validateCiScope({ ...manifest(), unrecognized: true }),
    /missing or unknown fields/,
  );
  const missing = manifest();
  delete missing.jobs.security;
  assert.throws(() => validateCiScope(missing), /missing or unknown fields/);
});

test("manifest validator rejects malformed paths, noncanonical selectors, and invalid job data", () => {
  const badPath = manifest();
  badPath.changedPaths[0].newPath = "../escape";
  assert.throws(() => validateCiScope(badPath), /Invalid changed path/);
  const unsorted = manifest();
  unsorted.targets.build = ["z-project", "a-project"];
  assert.throws(() => validateCiScope(unsorted), /Invalid project list for target build/);
  const badJob = manifest();
  badJob.jobs.workflowQuality.required = "false";
  assert.throws(() => validateCiScope(badJob), /Invalid job applicability/);
});

test("fallback manifests require every job and every declared target to be selected", () => {
  const fallback = manifest();
  fallback.validation.fallbackReason = "baseline unavailable";
  fallback.coverageFull = true;
  fallback.coverageComponents = ["shared-kernel"];
  for (const job of scopeJobs)
    fallback.jobs[job] = {
      required: true,
      reason: "fallback: baseline unavailable",
      expectedMatrix: [],
    };
  for (const target of scopeTargets) fallback.targets[target] = ["project"];
  assert.equal(validateCiScope(fallback).validation.fallbackReason, "baseline unavailable");
});
test("artifact validation binds manifest to expected head", () => {
  const directory = fs.mkdtempSync(path.join(os.tmpdir(), "ci-scope-"));
  const file = path.join(directory, "ci-scope.json");
  try {
    fs.writeFileSync(file, JSON.stringify(manifest()));
    assert.equal(readCiScope(file, "head-sha").head, "head-sha");
    assert.throws(() => readCiScope(file, "other-sha"), /does not match expected head/);
  } finally {
    fs.rmSync(directory, { recursive: true, force: true });
  }
});
test("coverage inventory and runner changes require full coverage selection", () => {
  const targets = Object.fromEntries(scopeTargets.map((target) => [target, []]));
  for (const changedPath of [
    "tools/coverage.mjs",
    "tools/coverage-lib.mjs",
    "tools/coverage-components.json",
  ]) {
    const changed = [{ status: "M", oldPath: changedPath, newPath: changedPath }];
    const jobs = getJobApplicability(changed, targets);
    assert.equal(jobs.coverage.required, true, changedPath);
    assert.equal(isCoverageFull(changed), true, changedPath);
  }
  assert.equal(
    isCoverageFull([{ status: "M", oldPath: "docs/ci.md", newPath: "docs/ci.md" }]),
    false,
  );
});
test("nonempty documentation changes require secret scanning but not full security scans", () => {
  const targets = Object.fromEntries(scopeTargets.map((target) => [target, []]));
  const jobs = getJobApplicability(
    [{ status: "M", oldPath: "docs/ci.md", newPath: "docs/ci.md" }],
    targets,
  );
  assert.equal(jobs.secretScan.required, true);
  assert.equal(jobs.security.required, false);
});

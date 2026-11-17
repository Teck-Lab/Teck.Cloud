import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import test from "node:test";
import { fileURLToPath } from "node:url";
import { parse } from "yaml";
import { requiredJobs, verifyCiResults } from "./check-ci-results.mjs";
import { scopeTargets } from "./ci-scope.mjs";

const success = () => Object.fromEntries(requiredJobs.map((name) => [name, { result: "success" }]));
const workflowText = fs.readFileSync(
  new URL("../.github/workflows/ci.yml", import.meta.url),
  "utf8",
);
const workflow = parse(workflowText);

function validScope() {
  return {
    schemaVersion: 1,
    event: "pull_request",
    head: "abc",
    validation: { base: "def", baselineSource: "test", fallbackReason: null },
    changedPaths: [],
    targets: Object.fromEntries(scopeTargets.map((target) => [target, []])),
    coverageComponents: [],
    coverageFull: false,
    jobs: Object.fromEntries(
      [
        "fast",
        "coverage",
        "aspire",
        "kubernetes",
        "images",
        "workflowQuality",
        "security",
        "secretScan",
      ].map((name) => [
        name,
        {
          required: name === "kubernetes",
          reason: "test",
          expectedMatrix: name === "kubernetes" ? ["full", "sandbox"] : [],
        },
      ]),
    ),
  };
}

test("required check preserves its name and manifest-selected lanes", () => {
  assert.equal(workflow.jobs.check.name, "Affected checks");
  assert.equal(workflow.jobs.check.if, "always()");
  assert.deepEqual([...workflow.jobs.check.needs].sort(), [...requiredJobs].sort());
  assert.deepEqual(
    Object.keys(workflow.jobs)
      .filter((name) => name !== "check")
      .sort(),
    [...requiredJobs].sort(),
  );
  for (const name of ["fast", "coverage", "aspire", "kubernetes", "images"]) {
    assert.match(workflow.jobs[name].if ?? "", /needs\.scope\.outputs/);
  }
  assert.equal(
    workflow.jobs.kubernetes.strategy.matrix.mode,
    `\${{ fromJSON(needs.scope.outputs.kubernetes_matrix) }}`,
  );
  const gateSteps = workflow.jobs.check.steps;
  const gateDownload = gateSteps.findIndex((step) =>
    step.uses?.startsWith("actions/download-artifact@"),
  );
  const gateSetup = gateSteps.findIndex((step) => step.uses?.startsWith("actions/setup-node@"));
  assert(gateDownload >= 0 && gateSetup > gateDownload, "gate must download before Node setup");
  for (const [name, job] of Object.entries(workflow.jobs).filter(
    ([name]) => name !== "scope" && name !== "check",
  )) {
    const steps = job.steps ?? [];
    const download = steps.findIndex((step) => step.uses?.startsWith("actions/download-artifact@"));
    const validate = steps.findIndex((step) => step.run?.includes("ci-scope.mjs --validate"));
    const setup = steps.findIndex(
      (step) =>
        step.uses?.startsWith("actions/setup-node@") ||
        step.uses?.startsWith("oven-sh/setup-bun@") ||
        step.uses?.startsWith("actions/setup-dotnet@"),
    );
    assert(
      download >= 0 && validate > download && setup > validate,
      `${name} must download and validate scope before setup`,
    );
  }
  assert.equal(workflowText.includes("--affected"), false);
  assert.equal(workflowText.includes("nx affected"), false);
  assert.equal(workflowText.includes("bun run affected:"), false);
});
test("coverage lane consumes full-mode manifest and skips expensive setup for no-op", () => {
  const scope = workflow.jobs.scope;
  assert.equal(scope.outputs.coverage_full, `\${{ steps.job-outputs.outputs.coverage_full }}`);
  const output = scope.steps.find((step) => step.name === "Export manifest job selection");
  assert.match(output.run, /\.coverageFull/);

  const steps = workflow.jobs.coverage.steps;
  const selector = steps.find(
    (step) => step.name === "Run coverage only when full or components were selected",
  );
  assert.match(selector.env.FULL, /coverage_full/);
  const setupDotnet = steps.find((step) => step.uses?.startsWith("actions/setup-dotnet@"));
  const restore = steps.find((step) => step.name === "Restore .NET projects");
  const image = steps.find(
    (step) => step.name === "Build the identity image used by integration tests",
  );
  const browser = steps.find(
    (step) => step.name === "Install browser for real Keycloak integration tests",
  );
  for (const step of [setupDotnet, restore, image, browser]) {
    assert.equal(step.if, "steps.coverage-selection.outputs.run == 'true'");
  }
  const execute = steps.find(
    (step) => step.name === "Enforce selected unit and integration coverage components",
  );
  assert.match(execute.run, /full\) bun run coverage -- --full/);
  assert.match(execute.run, /components\) bun run coverage -- --components/);
  assert.match(execute.run, /none\) bun run coverage -- --components '\[\]'/);
});
test("Security gate requires the separate content secret scan", () => {
  const securityText = fs.readFileSync(
    new URL("../.github/workflows/security.yml", import.meta.url),
    "utf8",
  );
  const security = parse(securityText);
  const scan = security.jobs["secret-scan"];
  assert.equal(scan.if, "needs.scope.outputs.secret_scan == 'true'");
  assert.match(
    scan.steps.find((step) => step.name === "Scan changed content or full history").run,
    /log_opts="\$SCAN_BASE\.\.\$SOURCE_SHA"/,
  );
  assert.match(
    scan.steps.find((step) => step.name === "Scan changed content or full history").run,
    /EVENT_NAME.*schedule.*workflow_dispatch/s,
  );
  assert.match(security.jobs.scope.outputs.scan_base, /steps.scope.outputs.scan_base/);
  const gate = security.jobs.gate;
  assert(gate.needs.includes("secret-scan"));
  const gateStep = gate.steps.find((step) => step.name === "Require all security jobs");
  assert.match(gateStep.run, /SECRET_SCAN/);
  assert.match(gateStep.run, /SECRET_REQUIRED/);
});

test("gate matches manifest-required jobs and expected matrix", () => {
  const scope = validScope();
  const results = success();
  for (const name of requiredJobs.filter((name) => name !== "scope")) {
    results[name].result = scope.jobs[name].required ? "success" : "skipped";
  }
  verifyCiResults(results, scope, { kubernetes: scope.jobs.kubernetes.expectedMatrix });
  const invalid = structuredClone(scope);
  invalid.jobs.kubernetes.expectedMatrix = ["bogus"];
  assert.throws(() => verifyCiResults(results, invalid, { kubernetes: [] }));
});

test("gate rejects unsuccessful lanes and malformed manifests", () => {
  const scope = validScope();
  const validResults = success();
  for (const name of requiredJobs.filter((name) => name !== "scope")) {
    validResults[name].result = scope.jobs[name].required ? "success" : "skipped";
  }
  for (const name of requiredJobs) {
    const failed = structuredClone(validResults);
    failed[name] = { result: "failure" };
    assert.throws(() => verifyCiResults(failed, scope, { kubernetes: ["full", "sandbox"] }));
    const missing = structuredClone(validResults);
    delete missing[name];
    assert.throws(() => verifyCiResults(missing, scope, { kubernetes: ["full", "sandbox"] }));
  }
  assert.throws(() => validateCiScope({ ...scope, targets: {} }));
});

test("workflow gate consumes run-bound manifest and fails closed", () => {
  const steps = workflow.jobs.check.steps;
  const download = steps.find((step) => step.uses?.startsWith("actions/download-artifact@"));
  assert.equal(download.with.name, `ci-scope-\${{ github.run_id }}-\${{ github.run_attempt }}`);
  const gate = steps.find((step) => step.name === "Require every validation lane");
  assert.equal(gate.env.CI_SCOPE_PATH, ".artifacts/ci-scope.json");
  const step = steps.find((candidate) => candidate.name === "Require every validation lane");
  assert.equal(step.run, "node tools/check-ci-results.mjs");
  assert.equal(step.env.RESULTS, `\${{ toJSON(needs) }}`);
  assert.equal(step.env.CI_SCOPE_PATH, ".artifacts/ci-scope.json");
  assert.equal(step.env.CI_SCOPE_EXPECTED_HEAD, `\${{ github.sha }}`);
});
test("gate CLI requires run-bound manifest", () => {
  const directory = fs.mkdtempSync(path.join(os.tmpdir(), "ci-scope-gate-"));
  const manifestPath = path.join(directory, "scope.json");
  const scope = validScope();
  const results = success();
  for (const name of requiredJobs.filter((name) => name !== "scope")) {
    results[name].result = scope.jobs[name].required ? "success" : "skipped";
  }
  fs.writeFileSync(manifestPath, JSON.stringify(scope));
  const run = (manifest, head = scope.head) =>
    spawnSync(
      process.execPath,
      [fileURLToPath(new URL("./check-ci-results.mjs", import.meta.url))],
      {
        env: {
          ...process.env,
          RESULTS: JSON.stringify(results),
          CI_SCOPE_PATH: manifest,
          CI_SCOPE_EXPECTED_HEAD: head,
          ACTUAL_MATRICES: JSON.stringify({ kubernetes: ["full", "sandbox"] }),
        },
        encoding: "utf8",
      },
    );
  assert.equal(run(manifestPath).status, 0);
  assert.notEqual(run(`${manifestPath}.missing`).status, 0);
  assert.notEqual(run(manifestPath, "other-head").status, 0);
  fs.rmSync(directory, { recursive: true, force: true });
});

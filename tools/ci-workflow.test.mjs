import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import fs from "node:fs";
import test from "node:test";
import { fileURLToPath } from "node:url";
import { parse } from "yaml";
import { requiredJobs, verifyCiResults } from "./check-ci-results.mjs";
import { isPublicationEnabled, requirePublicationEnabled } from "./release-activation.mjs";

const success = () => Object.fromEntries(requiredJobs.map((name) => [name, { result: "success" }]));
const workflow = parse(
  fs.readFileSync(new URL("../.github/workflows/ci.yml", import.meta.url), "utf8"),
);

test("required check preserves its name and includes every parallel lane", () => {
  assert.equal(workflow.jobs.check.name, "Affected checks");
  assert.equal(workflow.jobs.check.if, "always()");
  assert.deepEqual([...workflow.jobs.check.needs].sort(), [...requiredJobs].sort());
  assert.deepEqual(
    Object.keys(workflow.jobs)
      .filter((name) => name !== "check")
      .sort(),
    [...requiredJobs].sort(),
  );
  for (const name of requiredJobs.filter((name) => name !== "scope")) {
    assert.equal(workflow.jobs[name].needs, "scope");
    assert.equal(workflow.jobs[name].env.NX_BASE, `\${{ needs.scope.outputs.base }}`);
    assert.equal(workflow.jobs[name].env.NX_HEAD, `\${{ needs.scope.outputs.head }}`);
  }
  assert.deepEqual(workflow.jobs.kubernetes.strategy.matrix.mode, ["full", "sandbox"]);
  assert.equal(workflow.jobs.kubernetes.strategy["fail-fast"], false);
  assert.equal(workflow.jobs.kubernetes.env.TECK_KIND_MODE, `\${{ matrix.mode }}`);
});

test("gate accepts successful lanes, including a valid no-op affected selection", () => {
  const results = success();
  results.aspire.outputs = { affected: "[]" };
  verifyCiResults(results);
});

test("gate rejects every unsuccessful, absent and unexpected lane", () => {
  for (const name of requiredJobs) {
    for (const result of ["failure", "cancelled", "skipped", undefined]) {
      const results = success();
      results[name] = { result };
      assert.throws(() => verifyCiResults(results));
    }
    const missing = success();
    delete missing[name];
    assert.throws(() => verifyCiResults(missing));
  }
  for (const results of [null, [], {}, { ...success(), unvalidated: { result: "success" } }]) {
    assert.throws(() => verifyCiResults(results));
  }
});

test("workflow invokes the tested gate and CLI fails closed on malformed data", () => {
  const step = workflow.jobs.check.steps.find(
    (candidate) => candidate.name === "Require every validation lane",
  );
  assert.equal(step.run, "node tools/check-ci-results.mjs");
  assert.equal(step.env.RESULTS, `\${{ toJSON(needs) }}`);
  for (const [input, expected] of [
    [JSON.stringify(success()), 0],
    ["{}", 1],
    ["null", 1],
    ["invalid", 1],
  ]) {
    const result = spawnSync(
      process.execPath,
      [fileURLToPath(new URL("./check-ci-results.mjs", import.meta.url))],
      {
        env: { ...process.env, RESULTS: input },
        encoding: "utf8",
      },
    );
    assert.equal(result.status, expected, result.stderr);
  }
});

test("image publication requires the exact explicit activation value", () => {
  for (const value of [undefined, "", "false", "TRUE", "True", " true", "true ", "1"]) {
    assert.equal(isPublicationEnabled(value), false, `${value}`);
    assert.throws(() => requirePublicationEnabled(value), /ENABLE_IMAGE_PUBLICATION=true/);
  }
  assert.equal(isPublicationEnabled("true"), true);
  assert.equal(requirePublicationEnabled("true"), true);
});

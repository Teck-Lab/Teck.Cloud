import assert from "node:assert/strict";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { readCiScope, validateCiScope } from "./ci-scope.mjs";

export const requiredJobs = ["scope", "fast", "coverage", "aspire", "kubernetes", "images"];
const matrixJobs = { kubernetes: ["full", "sandbox"] };

export function verifyCiResults(
  results,
  scope,
  actualMatrices = { kubernetes: ["full", "sandbox"] },
) {
  assert(results && typeof results === "object" && !Array.isArray(results), "Missing CI results");
  validateCiScope(scope);
  assert.deepEqual(
    Object.keys(results).sort(),
    [...requiredJobs].sort(),
    "CI lanes are missing or unexpected",
  );
  for (const name of requiredJobs) {
    if (name === "scope") {
      assert.equal(results[name]?.result, "success", "Required CI lane scope did not succeed");
      continue;
    }
    const specification = scope.jobs?.[name];
    assert(
      specification && typeof specification.required === "boolean",
      `Manifest is missing job ${name}`,
    );
    const actual = results[name]?.result;
    if (specification.required) {
      assert.equal(actual, "success", `Required CI lane ${name} did not succeed`);
    } else {
      assert.equal(actual, "skipped", `Unrequired CI lane ${name} unexpectedly ran or failed`);
    }
    if (matrixJobs[name]) {
      const expected = specification.expectedMatrix;
      const actualMatrix = actualMatrices?.[name];
      assert(Array.isArray(expected), `Manifest matrix for ${name} is invalid`);
      assert(Array.isArray(actualMatrix), `Actual matrix for ${name} is missing`);
      assert(
        expected.every((mode) => matrixJobs[name].includes(mode)),
        `Manifest matrix for ${name} contains unknown entries`,
      );
      assert.deepEqual(
        [...actualMatrix].sort(),
        [...new Set(actualMatrix)].sort(),
        `Actual matrix for ${name} contains duplicates`,
      );
      assert.deepEqual(
        [...expected].sort(),
        [...actualMatrix].sort(),
        `Manifest matrix for ${name} does not match the actual matrix`,
      );
      assert.equal(
        Boolean(expected.length),
        Boolean(specification.required),
        `Manifest matrix disagrees with required ${name} lane`,
      );
    }
  }
}
if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const scope = readCiScope(
    process.env.CI_SCOPE_PATH ?? ".artifacts/ci-scope.json",
    process.env.CI_SCOPE_EXPECTED_HEAD,
  );
  verifyCiResults(
    JSON.parse(process.env.RESULTS ?? "null"),
    scope,
    JSON.parse(process.env.ACTUAL_MATRICES ?? "{}"),
  );
  console.log("CI results match the validated scope manifest.");
}

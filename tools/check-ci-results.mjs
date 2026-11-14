import assert from "node:assert/strict";
import path from "node:path";
import { fileURLToPath } from "node:url";

export const requiredJobs = ["scope", "fast", "coverage", "aspire", "kubernetes", "images"];

export function verifyCiResults(results) {
  assert(results && typeof results === "object" && !Array.isArray(results), "Missing CI results");
  assert.deepEqual(
    Object.keys(results).sort(),
    [...requiredJobs].sort(),
    "CI lanes are missing or unexpected",
  );
  for (const name of requiredJobs) {
    assert.equal(results[name]?.result, "success", `Required CI lane ${name} did not succeed`);
  }
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  verifyCiResults(JSON.parse(process.env.RESULTS ?? "null"));
  console.log("All required CI lanes succeeded.");
}

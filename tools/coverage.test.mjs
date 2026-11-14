import assert from "node:assert/strict";
import test from "node:test";
import { mergeReports, percentages, verifyBaseline } from "./coverage-lib.mjs";

const report = (hits) => ({
  "Example.dll": {
    "/repo/src/example.cs": {
      Example: {
        Method: {
          Lines: { 1: hits, 2: 0 },
          Branches: [
            { Offset: 1, Path: 0, Ordinal: 0, Hits: hits },
            { Offset: 1, Path: 1, Ordinal: 1, Hits: 0 },
          ],
        },
      },
    },
  },
});
test("merges coverage without double counting lines or branches", () => {
  assert.deepEqual(percentages(mergeReports([report(0), report(1)]), ["Example"]), {
    Example: { line: 50, branch: 50 },
  });
});
test("fails on missing assemblies and either kind of regression", () => {
  assert.throws(() => percentages(mergeReports([]), ["Missing"]));
  const baseline = { Example: { line: 50, branch: 50 } };
  assert.throws(() => verifyBaseline({}, baseline));
  assert.throws(() => verifyBaseline({ Example: { line: 49, branch: 60 } }, baseline));
  assert.throws(() => verifyBaseline({ Example: { line: 60, branch: 49 } }, baseline));
  verifyBaseline({ Example: { line: 60, branch: 60 } }, baseline);
});

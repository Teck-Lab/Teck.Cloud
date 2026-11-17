import assert from "node:assert/strict";
import test from "node:test";
import { selectFoundationCandidate } from "./select-foundation-candidate.mjs";

const sha = (digit) => digit.repeat(40);
const mkRun = (id, run_number, head_sha = sha("a"), overrides = {}) => ({
  id,
  run_number,
  run_attempt: 1,
  head_sha,
  status: "completed",
  conclusion: "success",
  path: ".github/workflows/foundation-candidate.yml",
  event: "workflow_dispatch",
  head_branch: "main",
  html_url: `https://github.com/Teck-Lab/Teck.Cloud/actions/runs/${id}`,
  repository: { full_name: "Teck-Lab/Teck.Cloud" },
  ...overrides,
});
const jobs = (attempt, overrides = {}) =>
  ["select", "build", "attest", "verify"].map((name) => ({
    name,
    conclusion: "success",
    run_attempt: attempt,
    ...overrides[name],
  }));
const gitAncestor = () => ({ status: 0, stdout: "", stderr: "" });
const args = (overrides = {}) => ({
  runs: [
    mkRun(10, 1, sha("a")),
    mkRun(11, 2, sha("b"), { status: "in_progress", conclusion: null }),
  ],
  jobsByAttempt: { "10:1": jobs(1) },
  repository: "Teck-Lab/Teck.Cloud",
  workflowRunId: 11,
  currentRunNumber: 2,
  requestedHead: sha("c"),
  changedPaths: ["docs/ci.md"],
  git: gitAncestor,
  ...overrides,
});

test("verified candidate baseline makes unrelated change an explicit no-op", () => {
  const result = selectFoundationCandidate(args());
  assert.equal(result.selected, false);
  assert.equal(result.reason, "no-artifact-inputs-changed");
  assert.deepEqual(result.baseline, {
    runId: 10,
    runAttempt: 1,
    runUrl: "https://github.com/Teck-Lab/Teck.Cloud/actions/runs/10",
    sourceSha: sha("a"),
  });
});

test("first candidate and history without a complete successful candidate select all inputs", () => {
  assert.equal(
    selectFoundationCandidate(args({ runs: [mkRun(11, 2)], jobsByAttempt: {} })).reason,
    "initial-candidate",
  );
  const partial = { ...mkRun(10, 1), conclusion: "failure" };
  assert.equal(
    selectFoundationCandidate(args({ runs: [partial, mkRun(11, 2)], jobsByAttempt: {} })).selected,
    true,
  );
});

test("actual foundation inputs select the candidate", () => {
  const result = selectFoundationCandidate(
    args({ changedPaths: ["src/shared/SharedKernel.Core/Thing.cs"] }),
  );
  assert.equal(result.selected, true);
  assert.equal(result.reason, "artifact-inputs-changed");
});

test("newest complete successful candidate is the only baseline considered", () => {
  const newest = mkRun(12, 3, sha("d"));
  const result = selectFoundationCandidate(
    args({
      runs: [
        mkRun(10, 1),
        newest,
        mkRun(11, 4, sha("e"), { status: "in_progress", conclusion: null }),
        mkRun(13, 5, sha("f"), { status: "in_progress", conclusion: null }),
      ],
      jobsByAttempt: { "10:1": jobs(1), "12:1": jobs(1) },
      workflowRunId: 13,
      currentRunNumber: 5,
      git: (gitArgs) =>
        gitArgs[2] === sha("d") ? gitAncestor() : { status: 1, stdout: "", stderr: "" },
    }),
  );
  assert.equal(result.baseline.runId, 12);
});

test("successful run missing job data and invalid ancestry fail closed", () => {
  assert.throws(() => selectFoundationCandidate(args({ jobsByAttempt: {} })), /Missing jobs/);
  assert.throws(
    () => selectFoundationCandidate(args({ git: () => ({ status: 1, stdout: "", stderr: "" }) })),
    /not an ancestor/,
  );
});

test("untrusted run identity and ambiguous job identity fail closed", () => {
  assert.throws(
    () =>
      selectFoundationCandidate(
        args({ runs: [mkRun(10, 1, sha("a"), { head_branch: "topic" }), mkRun(11, 2)] }),
      ),
    /untrusted/,
  );
  assert.throws(
    () =>
      selectFoundationCandidate(
        args({
          jobsByAttempt: {
            "10:1": [...jobs(1), { name: "verify", conclusion: "success", run_attempt: 1 }],
          },
        }),
      ),
    /Ambiguous/,
  );
});

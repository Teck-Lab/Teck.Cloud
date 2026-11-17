import { spawnSync } from "node:child_process";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const repoRoot = fileURLToPath(new URL("../", import.meta.url));
const inputContract = JSON.parse(
  fs.readFileSync(new URL("./release-inputs.json", import.meta.url), "utf8"),
);
const expectedJobs = ["build", "attest", "verify"];

function fail(message) {
  throw new Error(message);
}

function validSha(value) {
  return typeof value === "string" && /^[0-9a-f]{40}$/i.test(value);
}

function isAncestor(ancestor, head, git) {
  const result = git(["merge-base", "--is-ancestor", ancestor, head]);
  if (result.error) fail(`git merge-base could not be started: ${result.error.message}`);
  if (result.status === 0) return true;
  if (result.status === 1) return false;
  fail(`git ancestry check failed: ${result.stderr || result.stdout}`);
}

function normalizeRun(run, repository) {
  if (
    !run ||
    run.repository?.full_name !== repository ||
    run.path !== ".github/workflows/foundation-candidate.yml" ||
    run.event !== "workflow_dispatch" ||
    run.head_branch !== "main" ||
    !validSha(run.head_sha) ||
    !Number.isInteger(run.run_number) ||
    !Number.isInteger(run.id)
  ) {
    fail("Foundation run has an untrusted or malformed identity");
  }
  return run;
}

function successfulCompleteAttempt(jobs, attempt) {
  if (!Array.isArray(jobs)) fail("Could not read Foundation candidate attempt jobs");
  const byName = new Map();
  for (const job of jobs) {
    if (typeof job.name !== "string" || !job.name) fail("Foundation candidate has an unnamed job");
    const name = job.name.split(" / ").at(-1);
    if ([...expectedJobs, "select"].includes(name)) {
      if (byName.has(name)) fail(`Ambiguous Foundation candidate job identity: ${name}`);
      byName.set(name, job);
    }
  }
  if (!expectedJobs.every((name) => byName.has(name)))
    fail("Unrecognized successful Foundation candidate job layout");
  if (byName.has("select") && byName.get("select").run_attempt !== attempt)
    fail("Foundation select job belongs to another attempt");
  if ([...byName.values()].some((job) => job.run_attempt !== attempt))
    fail("Foundation jobs do not belong to one run attempt");
  if (byName.has("select") && byName.get("select").conclusion !== "success") return false;
  const results = expectedJobs.map((name) => byName.get(name).conclusion);
  if (results.every((result) => result === "success")) return true;
  if (results.every((result) => result === "skipped")) return false;
  if (results.every((result) => result === "skipped" || result === "success")) return false;
  fail("Foundation candidate attempt has contradictory job results");
}

function matchesAny(file, patterns) {
  return patterns.some((pattern) => {
    const expression = pattern
      .split("**/")
      .map((part) =>
        part
          .split("*")
          .map((literal) => literal.replace(/[|\\{}()[\]^$+?.]/g, "\\$&"))
          .join("[^/]*"),
      )
      .join("(?:.*/)?");
    return new RegExp(`^${expression}$`).test(file);
  });
}

export function selectFoundationCandidate({
  runs,
  jobsByAttempt,
  repository,
  workflowRunId,
  currentRunNumber,
  requestedHead,
  changedPaths,
  git,
}) {
  if (
    !Array.isArray(runs) ||
    (changedPaths !== undefined && !Array.isArray(changedPaths)) ||
    typeof repository !== "string" ||
    !repository ||
    !Number.isInteger(workflowRunId) ||
    !Number.isInteger(currentRunNumber) ||
    !validSha(requestedHead) ||
    typeof git !== "function"
  ) {
    fail("Invalid candidate selection inputs");
  }
  if (
    inputContract.schemaVersion !== 1 ||
    !inputContract.artifacts?.["foundation-bundle"] ||
    !Array.isArray(inputContract.artifacts["foundation-bundle"].inputPatterns)
  ) {
    fail("Invalid release input contract");
  }
  const orderedRuns = runs
    .map((run) => normalizeRun(run, repository))
    .filter((run) => run.id !== workflowRunId);
  orderedRuns.sort((a, b) => b.run_number - a.run_number || b.id - a.id);
  let baseline = null;
  for (const run of orderedRuns) {
    if (run.run_number < 1 || !Number.isInteger(run.run_attempt) || run.run_attempt < 1)
      fail("Invalid Foundation run attempt metadata");
    if (run.run_number >= currentRunNumber) continue;
    if (run.status !== "completed" || run.conclusion !== "success") continue;
    const attemptKey = `${run.id}:${run.run_attempt}`;
    if (!Object.hasOwn(jobsByAttempt ?? {}, attemptKey))
      fail(`Missing jobs for successful Foundation attempt ${attemptKey}`);
    if (!successfulCompleteAttempt(jobsByAttempt[attemptKey], run.run_attempt)) continue;
    if (!isAncestor(run.head_sha, requestedHead, git))
      fail("Verified Foundation baseline is not an ancestor of requested source");
    baseline = {
      runId: run.id,
      runAttempt: run.run_attempt,
      runUrl: run.html_url,
      sourceSha: run.head_sha,
    };
    break;
  }
  const baselinePaths =
    changedPaths ??
    (baseline
      ? (() => {
          const result = git(["diff", "--name-only", `${baseline.sourceSha}...${requestedHead}`]);
          if (result.error || result.status !== 0)
            fail(
              `Could not compare candidate inputs: ${result.stderr || result.error?.message || result.stdout}`,
            );
          return result.stdout.split(/\r?\n/).filter(Boolean);
        })()
      : []);
  const changed = [
    ...new Set(
      baselinePaths.map((value) => {
        if (
          typeof value !== "string" ||
          !value ||
          path.posix.isAbsolute(value) ||
          value.includes("..") ||
          value.includes("\\")
        )
          fail("Invalid changed path");
        return value;
      }),
    ),
  ].sort();
  const selected =
    baseline === null ||
    changed.some((file) =>
      matchesAny(file, inputContract.artifacts["foundation-bundle"].inputPatterns),
    );
  return {
    schemaVersion: 1,
    artifact: "foundation-bundle",
    head: requestedHead,
    baseline,
    reason: selected
      ? baseline
        ? "artifact-inputs-changed"
        : "initial-candidate"
      : "no-artifact-inputs-changed",
    selected,
    changedInputs: selected
      ? changed.filter((file) =>
          matchesAny(file, inputContract.artifacts["foundation-bundle"].inputPatterns),
        )
      : [],
  };
}

function main() {
  const input = JSON.parse(fs.readFileSync(process.env.SELECTION_INPUT, "utf8"));
  const result = selectFoundationCandidate({
    ...input,
    git: (args) => spawnSync("git", args, { cwd: repoRoot, encoding: "utf8" }),
  });
  fs.mkdirSync(path.dirname(process.env.GITHUB_OUTPUT), { recursive: true });
  fs.appendFileSync(
    process.env.GITHUB_OUTPUT,
    `selected=${result.selected}\nreason=${result.reason}\n`,
  );
  fs.mkdirSync(path.dirname(process.env.MANIFEST_OUTPUT), { recursive: true });
  fs.writeFileSync(process.env.MANIFEST_OUTPUT, `${JSON.stringify(result, null, 2)}\n`);
  console.log(
    result.selected
      ? `Foundation candidate selected: ${result.reason}`
      : "Foundation candidate no-op: no artifact inputs changed",
  );
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  try {
    main();
  } catch (error) {
    console.error(error.message);
    process.exitCode = 1;
  }
}

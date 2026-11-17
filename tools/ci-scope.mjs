import { spawnSync } from "node:child_process";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

export const scopeSchemaVersion = 1;
export const scopeJobs = [
  "fast",
  "coverage",
  "aspire",
  "kubernetes",
  "images",
  "workflowQuality",
  "security",
  "secretScan",
];
export const scopeTargets = [
  "build",
  "lint",
  "typecheck",
  "test",
  "test:integration",
  "test:e2e",
  "test:kubernetes",
  "container:build",
  "verify:static",
];
export const scopeFields = [
  "schemaVersion",
  "event",
  "head",
  "validation",
  "changedPaths",
  "targets",
  "coverageFull",
  "coverageComponents",
  "jobs",
];
const contract = JSON.parse(
  fs.readFileSync(new URL("./ci-scope-contracts.json", import.meta.url), "utf8"),
);
const coverageContracts = JSON.parse(
  fs.readFileSync(new URL("./coverage-components.json", import.meta.url), "utf8"),
);

function run(command, args, cwd) {
  const result = spawnSync(command, args, { cwd, encoding: "utf8", maxBuffer: 32 * 1024 * 1024 });
  if (result.error) throw new Error(`${command} could not be started: ${result.error.message}`);
  if (result.status !== 0)
    throw new Error(
      `${command} failed (${result.status}): ${(result.stderr || result.stdout).trim()}`,
    );
  return result.stdout;
}

function normalizePath(value) {
  if (
    typeof value !== "string" ||
    !value ||
    value.includes("\0") ||
    path.posix.isAbsolute(value) ||
    /^[A-Za-z]:/.test(value)
  )
    throw new Error(`Invalid changed path: ${JSON.stringify(value)}`);
  const normalized = value.replaceAll("\\", "/");
  if (normalized.split("/").some((segment) => segment === ".." || segment === ""))
    throw new Error(`Invalid changed path: ${JSON.stringify(value)}`);
  return normalized;
}

function parseNameStatus(output) {
  const fields = output.split("\0");
  if (fields.at(-1) === "") fields.pop();
  const records = [];
  for (let i = 0; i < fields.length; ) {
    const status = fields[i++];
    if (!/^(?:A|C|D|M|R|T|U|X|B)(?:\d{1,3})?$/.test(status ?? ""))
      throw new Error(`Malformed git name-status output at field ${i}`);
    let oldPath;
    let newPath;
    if (status[0] === "A") {
      oldPath = null;
      newPath = normalizePath(fields[i++]);
    } else if (status[0] === "D") {
      oldPath = normalizePath(fields[i++]);
      newPath = null;
    } else if (status[0] === "R" || status[0] === "C") {
      oldPath = normalizePath(fields[i++]);
      newPath = normalizePath(fields[i++]);
    } else {
      oldPath = normalizePath(fields[i++]);
      newPath = oldPath;
    }
    if (oldPath === undefined || newPath === undefined)
      throw new Error("Malformed git name-status record");
    records.push({ status, oldPath, newPath });
  }
  return records.sort(
    (a, b) =>
      (a.oldPath ?? "").localeCompare(b.oldPath ?? "") ||
      (a.newPath ?? "").localeCompare(b.newPath ?? "") ||
      a.status.localeCompare(b.status),
  );
}

function selectedTargets({ base, head, fallbackReason, cwd, nx }) {
  const targets = Object.fromEntries(scopeTargets.map((target) => [target, []]));
  for (const target of scopeTargets) {
    const args = ["run", "nx", "show", "projects"];
    if (!fallbackReason) args.push("--affected", `--base=${base}`, `--head=${head}`);
    args.push(`--with-target=${target}`, "--json");
    let names;
    try {
      names = JSON.parse(run(nx, args, cwd));
    } catch (error) {
      throw new Error(`Nx returned invalid project list for ${target}: ${error.message}`);
    }
    if (!Array.isArray(names) || names.some((name) => typeof name !== "string" || !name))
      throw new Error(`Nx returned invalid project list for ${target}`);
    targets[target] = [...new Set(names)].sort();
  }
  return targets;
}

export function isCoverageFull(paths, fallbackReason = null) {
  return (
    Boolean(fallbackReason) ||
    paths
      .flatMap(({ oldPath, newPath }) => [oldPath, newPath].filter(Boolean))
      .some((p) => contract.coverageFullInputs.some((pattern) => new RegExp(pattern).test(p)))
  );
}
export function getJobApplicability(paths, targets, fallbackReason = null) {
  const allPaths = paths.flatMap(({ oldPath, newPath }) => [oldPath, newPath].filter(Boolean));
  const config = allPaths.some((p) =>
    contract.globalInputs.some((pattern) => new RegExp(pattern).test(p)),
  );
  const source = allPaths.some((p) => p.startsWith("src/") || p.startsWith("tests/"));
  const app = allPaths.some((p) => /^(?:src\/apps|tests\/(?:integration|e2e))\//.test(p));
  const infra = allPaths.some((p) => /^(?:deployment|tests\/kubernetes)\//.test(p));
  const artifact =
    targets["container:build"].length > 0 || allPaths.some((p) => p.startsWith("src/"));
  const selected = {
    fast:
      config ||
      source ||
      targets.build.length > 0 ||
      targets.lint.length > 0 ||
      targets.typecheck.length > 0 ||
      targets.test.length > 0 ||
      targets["verify:static"].length > 0,
    coverage:
      config ||
      source ||
      targets["test:integration"].length > 0 ||
      isCoverageFull(paths, fallbackReason),
    aspire: config || app || targets["test:e2e"].length > 0,
    kubernetes: config || infra || targets["test:kubernetes"].length > 0,
    images: config || artifact,
    workflowQuality:
      config ||
      allPaths.some(
        (p) => p.startsWith(".github/") || p.startsWith("tools/") || p.startsWith("docs/"),
      ),
    security:
      config ||
      allPaths.some((p) => contract.securityInputs.some((pattern) => new RegExp(pattern).test(p))),
    secretScan: allPaths.length > 0,
  };
  const jobs = {};
  for (const job of scopeJobs) {
    const required = Boolean(fallbackReason) || selected[job];
    jobs[job] = {
      required,
      reason: fallbackReason
        ? `fallback: ${fallbackReason}`
        : required
          ? "applicable changes detected"
          : "no applicable changes",
      expectedMatrix: job === "kubernetes" && required ? ["full", "sandbox"] : [],
    };
  }
  return jobs;
}

/** Resolve scope with explicit event/base/head; Nx affected graph is queried once. */
export function resolveCiScope({
  event,
  base,
  head,
  baselineSource,
  fallbackReason = null,
  cwd = repoRoot,
  nx = "bun",
}) {
  for (const [name, value] of Object.entries({ event, base, head, baselineSource }))
    if (typeof value !== "string" || !value || value.includes("\0"))
      throw new TypeError(`${name} must be a non-empty string`);
  if (fallbackReason !== null && (typeof fallbackReason !== "string" || !fallbackReason.trim()))
    throw new TypeError("fallbackReason must be null or a non-empty string");
  const changedPaths = parseNameStatus(
    run("git", ["diff", "--name-status", "-z", "--find-renames", base, head], cwd),
  );
  const targets = selectedTargets({ base, head, fallbackReason, cwd, nx });
  const paths = changedPaths.flatMap(({ oldPath, newPath }) => [oldPath, newPath].filter(Boolean));
  const jobs = getJobApplicability(changedPaths, targets, fallbackReason);
  const coverageFull = isCoverageFull(changedPaths, fallbackReason);
  const coverageComponents = coverageFull
    ? Object.keys(coverageContracts.components)
    : Object.keys(coverageContracts.components).filter((id) =>
        paths.some((p) => {
          const component = coverageContracts.components[id];
          return (
            component.projects.some(
              ({ path: projectPath }) =>
                p === projectPath || p.startsWith(`${path.posix.dirname(projectPath)}/`),
            ) ||
            component.suites.some(
              (suite) => p === suite || p.startsWith(`${path.posix.dirname(suite)}/`),
            )
          );
        }),
      );
  return validateCiScope({
    schemaVersion: scopeSchemaVersion,
    event,
    head,
    validation: { base, baselineSource, fallbackReason },
    changedPaths,
    targets,
    coverageFull,
    coverageComponents: coverageComponents.sort(),
    jobs,
  });
}
export function validateCiScope(value) {
  const exact = (actual, expected, label) => {
    if (
      !actual ||
      typeof actual !== "object" ||
      Array.isArray(actual) ||
      Object.keys(actual).sort().join("\0") !== [...expected].sort().join("\0")
    )
      throw new Error(`${label} has missing or unknown fields`);
  };
  exact(value, scopeFields, "manifest");
  if (value.schemaVersion !== scopeSchemaVersion)
    throw new Error("Unsupported scope schemaVersion");
  exact(value.validation, ["base", "baselineSource", "fallbackReason"], "validation");
  if (
    typeof value.validation.base !== "string" ||
    !value.validation.base ||
    typeof value.validation.baselineSource !== "string" ||
    !value.validation.baselineSource ||
    (value.validation.fallbackReason !== null &&
      (typeof value.validation.fallbackReason !== "string" ||
        !value.validation.fallbackReason.trim()))
  )
    throw new Error("Invalid validation metadata");
  if (!Array.isArray(value.changedPaths)) throw new Error("changedPaths must be an array");
  const pathKeys = [];
  for (const item of value.changedPaths) {
    exact(item, ["status", "oldPath", "newPath"], "changed path");
    if (typeof item.status !== "string" || !/^(?:A|C|D|M|R|T|U|X|B)(?:\d{1,3})?$/.test(item.status))
      throw new Error("Invalid changed path status");
    if (item.oldPath !== null) normalizePath(item.oldPath);
    if (item.newPath !== null) normalizePath(item.newPath);
    if (
      (item.status[0] === "A") !== (item.oldPath === null) ||
      (item.status[0] === "D") !== (item.newPath === null)
    )
      throw new Error("Status and path nullability disagree");
    pathKeys.push(`${item.oldPath ?? ""}\0${item.newPath ?? ""}\0${item.status}`);
  }
  if (!isSortedUnique(pathKeys)) throw new Error("changedPaths must be sorted and unique");
  exact(value.targets, scopeTargets, "targets");
  for (const target of scopeTargets)
    if (
      !Array.isArray(value.targets[target]) ||
      value.targets[target].some((p) => typeof p !== "string" || !p) ||
      !isSortedUnique(value.targets[target])
    )
      throw new Error(`Invalid project list for target ${target}`);
  if (typeof value.coverageFull !== "boolean") throw new Error("coverageFull must be boolean");
  if (
    !Array.isArray(value.coverageComponents) ||
    value.coverageComponents.some((id) => typeof id !== "string" || !id) ||
    !isSortedUnique(value.coverageComponents) ||
    value.coverageComponents.some((id) => !Object.hasOwn(coverageContracts.components, id))
  )
    throw new Error("Invalid coverageComponents");
  if (
    value.coverageFull &&
    value.coverageComponents.length !== Object.keys(coverageContracts.components).length
  )
    throw new Error("Full coverage must include every coverage component");
  exact(value.jobs, scopeJobs, "jobs");
  for (const job of scopeJobs) {
    exact(value.jobs[job], ["required", "reason", "expectedMatrix"], `job ${job}`);
    if (
      typeof value.jobs[job].required !== "boolean" ||
      typeof value.jobs[job].reason !== "string" ||
      !value.jobs[job].reason ||
      !Array.isArray(value.jobs[job].expectedMatrix) ||
      value.jobs[job].expectedMatrix.some((item) => typeof item !== "string" || !item) ||
      !isSortedUnique(value.jobs[job].expectedMatrix)
    )
      throw new Error(`Invalid job applicability for ${job}`);
  }
  if (
    value.validation.fallbackReason &&
    (!value.coverageFull || scopeJobs.some((job) => !value.jobs[job].required))
  )
    throw new Error("Fallback scope must require all jobs and full coverage");
  return value;
}

function isSortedUnique(values) {
  return values.every((value, index) => index === 0 || values[index - 1].localeCompare(value) < 0);
}

export function writeCiScope(scope, output = path.join(repoRoot, ".artifacts", "ci-scope.json")) {
  validateCiScope(scope);
  fs.mkdirSync(path.dirname(output), { recursive: true });
  fs.writeFileSync(output, `${JSON.stringify(scope, null, 2)}\n`, { flag: "w" });
}

export function readCiScope(input, expectedHead = process.env.CI_SCOPE_EXPECTED_HEAD) {
  const scope = validateCiScope(JSON.parse(fs.readFileSync(input, "utf8")));
  if (expectedHead && scope.head !== expectedHead)
    throw new Error(`Scope head ${scope.head} does not match expected head ${expectedHead}`);
  return scope;
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  if (process.argv[2] === "--validate") {
    const input =
      process.argv[3] && process.argv[3] !== "--head"
        ? process.argv[3]
        : path.join(repoRoot, ".artifacts", "ci-scope.json");
    const headFlag = process.argv.indexOf("--head");
    readCiScope(
      input,
      headFlag >= 0 ? process.argv[headFlag + 1] : process.env.CI_SCOPE_EXPECTED_HEAD,
    );
  } else {
    writeCiScope(
      resolveCiScope({
        event: process.env.CI_EVENT,
        base: process.env.NX_BASE,
        head: process.env.NX_HEAD,
        baselineSource: process.env.CI_SCOPE_BASELINE_SOURCE,
        fallbackReason: process.env.CI_SCOPE_FALLBACK_REASON || null,
      }),
    );
  }
}

import assert from "node:assert/strict";
import { execFileSync } from "node:child_process";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { mergeReports, percentages, verifyBaseline } from "./coverage-lib.mjs";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
process.chdir(root);
const output = path.join(root, ".artifacts", "coverage");
const baselinePath = "tools/coverage-baseline.json";
function files(directory, suffix) {
  return fs.readdirSync(directory, { withFileTypes: true }).flatMap((entry) => {
    if (["bin", "obj"].includes(entry.name)) return [];
    const full = path.join(directory, entry.name);
    return entry.isDirectory() ? files(full, suffix) : full.endsWith(suffix) ? [full] : [];
  });
}
const projects = ["tests/unit", "tests/integration"].flatMap((dir) => files(dir, ".csproj")).sort();
const assemblies = ["src/shared", "src/services"]
  .flatMap((dir) => files(dir, ".csproj"))
  .map((file) => {
    const xml = fs.readFileSync(file, "utf8");
    return (
      xml.match(/<AssemblyName>([^<]+)<\/AssemblyName>/)?.[1] ?? path.basename(file, ".csproj")
    );
  })
  .sort();
assert(projects.length && assemblies.length, "Coverage scope must not be empty");
if (!process.argv.includes("--reports-only")) {
  // Fixed, repository-owned output directory, never a user-supplied deletion path.
  fs.rmSync(output, { recursive: true, force: true });
  fs.mkdirSync(output, { recursive: true });
  execFileSync("dotnet", ["build", "Teck.Cloud.slnx", "-c", "Release"], { stdio: "inherit" });
  for (const project of projects) {
    execFileSync(
      "dotnet",
      [
        "run",
        "--no-build",
        "-c",
        "Release",
        "--project",
        project,
        "--",
        "--coverlet",
        "--coverlet-output-format",
        "json",
        ...assemblies.flatMap((name) => ["--coverlet-include", `[${name}]*`]),
        "--results-directory",
        path.join(output, path.basename(project, ".csproj")),
      ],
      { stdio: "inherit", timeout: 600_000 },
    );
  }
}
const reports = projects.flatMap((project) => {
  const dir = path.join(output, path.basename(project, ".csproj"));
  const reports = files(dir, ".json").filter((file) => path.basename(file).startsWith("coverage."));
  assert.equal(reports.length, 1, `Expected exactly one fresh coverage report for ${project}`);
  return reports.map((file) => JSON.parse(fs.readFileSync(file, "utf8")));
});
const result = percentages(mergeReports(reports), assemblies);
fs.writeFileSync(path.join(output, "summary.json"), `${JSON.stringify(result, null, 2)}\n`);
let previous;
if (process.env.NX_BASE) {
  // The initial gate has no baseline in the base revision. Other Git errors still fail.
  const listing = execFileSync("git", ["ls-tree", process.env.NX_BASE, "--", baselinePath], {
    encoding: "utf8",
  });
  if (listing.trim())
    previous = JSON.parse(
      execFileSync("git", ["show", `${process.env.NX_BASE}:${baselinePath}`], { encoding: "utf8" }),
    );
}
if (process.argv.includes("--update-baseline")) {
  if (fs.existsSync(baselinePath))
    verifyBaseline(result, JSON.parse(fs.readFileSync(baselinePath, "utf8")));
  if (previous) verifyBaseline(result, previous);
  fs.writeFileSync(baselinePath, `${JSON.stringify(result, null, 2)}\n`);
} else {
  const baseline = JSON.parse(fs.readFileSync(baselinePath, "utf8"));
  if (previous) verifyBaseline(baseline, previous);
  verifyBaseline(result, baseline);
  assert.deepEqual(
    result,
    baseline,
    "Coverage improved or scope changed: run bun run coverage:update and include the raised baseline",
  );
}
console.table(result);

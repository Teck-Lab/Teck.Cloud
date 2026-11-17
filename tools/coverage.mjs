import assert from "node:assert/strict";
import { execFileSync } from "node:child_process";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import {
  loadComponents,
  mergeReports,
  percentages,
  resolveCoverageMode,
  verifyBaseline,
} from "./coverage-lib.mjs";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
process.chdir(root);
const baselinePath = "tools/coverage-baseline.json";
const outputRoot = path.join(root, ".artifacts", "coverage");
function files(directory, suffix) {
  return fs.readdirSync(directory, { withFileTypes: true }).flatMap((entry) => {
    if (["bin", "obj"].includes(entry.name)) return [];
    const full = path.join(directory, entry.name);
    return entry.isDirectory() ? files(full, suffix) : full.endsWith(suffix) ? [full] : [];
  });
}
const suites = ["tests/unit", "tests/integration"]
  .flatMap((dir) => files(dir, ".csproj"))
  .map((p) => p.split(path.sep).join("/"))
  .sort();
const projectFiles = ["src/shared", "src/services"]
  .flatMap((dir) => files(dir, ".csproj"))
  .map((p) => p.split(path.sep).join("/"))
  .sort();
const assembliesByProject = Object.fromEntries(
  projectFiles.map((file) => {
    const xml = fs.readFileSync(file, "utf8");
    return [
      file,
      xml.match(/<AssemblyName>([^<]+)<\/AssemblyName>/)?.[1] ?? path.basename(file, ".csproj"),
    ];
  }),
);
const projectAssemblies = Object.fromEntries(
  Object.entries(assembliesByProject).map(([file, assembly]) => [assembly, file]),
);
const components = loadComponents(
  JSON.parse(fs.readFileSync("tools/coverage-components.json", "utf8")),
  { projects: projectFiles, suites, assemblies: assembliesByProject },
);
const selection = resolveCoverageMode(process.argv.slice(2), components);
const selectedNames = selection.mode === "full" ? Object.keys(components) : selection.names;
const selectedSuites =
  selection.mode === "full"
    ? suites
    : [...new Set(selectedNames.flatMap((name) => components[name].suites))];
const selectedAssemblies =
  selection.mode === "full"
    ? Object.values(assembliesByProject)
    : [...new Set(selectedNames.flatMap((name) => components[name].assemblies))];
let previous;
if (process.env.NX_BASE) {
  const listing = execFileSync("git", ["ls-tree", process.env.NX_BASE, "--", baselinePath], {
    encoding: "utf8",
  });
  if (listing.trim())
    previous = JSON.parse(
      execFileSync("git", ["show", `${process.env.NX_BASE}:${baselinePath}`], { encoding: "utf8" }),
    );
}
const currentBaseline = JSON.parse(fs.readFileSync(baselinePath, "utf8"));
if (previous) verifyBaseline(currentBaseline, previous);
if (selection.mode === "components" && selectedNames.length === 0) {
  console.log("Coverage: no components selected (no-op).");
  process.exit(0);
}
const scopeAssemblies = [
  ...new Set([
    ...selectedAssemblies,
    ...(selectedSuites.includes("tests/unit/AppHost.UnitTests/AppHost.UnitTests.csproj")
      ? ["Teck.Cloud.AppHost"]
      : []),
  ]),
];
assert(
  scopeAssemblies.every((name) => projectAssemblies[name] || name === "Teck.Cloud.AppHost"),
  "Coverage references an unknown assembly",
);
const output = path.join(
  outputRoot,
  selection.mode === "full"
    ? "full"
    : `components-${selectedNames.map(encodeURIComponent).join("+")}`,
);
if (!process.argv.includes("--reports-only")) {
  fs.rmSync(output, { recursive: true, force: true });
  fs.mkdirSync(output, { recursive: true });
  for (const project of selectedSuites)
    execFileSync("dotnet", ["build", project, "-c", "Release"], { stdio: "inherit" });
  for (const project of selectedSuites) {
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
        ...scopeAssemblies.flatMap((name) => ["--coverlet-include", `[${name}]*`]),
        "--results-directory",
        path.join(output, encodeURIComponent(project)),
      ],
      { stdio: "inherit", timeout: 600_000 },
    );
  }
}
const reports = selectedSuites.flatMap((project) => {
  const dir = path.join(output, encodeURIComponent(project));
  const found = files(dir, ".json").filter((file) => path.basename(file).startsWith("coverage."));
  assert.equal(found.length, 1, `Expected exactly one fresh coverage report for ${project}`);
  return found.map((file) => JSON.parse(fs.readFileSync(file, "utf8")));
});
const result = percentages(mergeReports(reports), selectedAssemblies);
fs.writeFileSync(path.join(output, "summary.json"), `${JSON.stringify(result, null, 2)}\n`);
if (selection.mode === "full") {
  if (process.argv.includes("--update-baseline")) {
    if (fs.existsSync(baselinePath))
      verifyBaseline(result, JSON.parse(fs.readFileSync(baselinePath, "utf8")));
    if (previous) verifyBaseline(result, previous);
    fs.writeFileSync(baselinePath, `${JSON.stringify(result, null, 2)}\n`);
  } else {
    verifyBaseline(result, currentBaseline);
    assert.deepEqual(
      result,
      currentBaseline,
      "Coverage improved or scope changed: run bun run coverage:update and include the raised baseline",
    );
  }
} else {
  verifyBaseline(
    result,
    Object.fromEntries(
      selectedAssemblies.map((assembly) => {
        assert(
          currentBaseline[assembly],
          `Coverage baseline is missing selected assembly ${assembly}`,
        );
        return [assembly, currentBaseline[assembly]];
      }),
    ),
  );
}
console.table(result);

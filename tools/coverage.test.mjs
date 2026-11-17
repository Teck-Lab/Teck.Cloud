import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";
import {
  loadComponents,
  mergeReports,
  percentages,
  resolveCoverageMode,
  verifyBaseline,
} from "./coverage-lib.mjs";

const manifest = JSON.parse(
  readFileSync(new URL("./coverage-components.json", import.meta.url), "utf8"),
);
const projects = manifest.components["shared-kernel"].projects.map((p) => p.path);
const suites = manifest.components["shared-kernel"].suites;
const assemblies = Object.fromEntries(
  manifest.components["shared-kernel"].projects.map((p) => [p.path, p.assembly]),
);
test("validates full, empty, and multi-component selections", () => {
  const components = loadComponents(manifest, { projects, suites, assemblies });
  assert.deepEqual(resolveCoverageMode([], components), { mode: "full", names: [] });
  const selections = {
    ...components,
    payments: { suites: ["payments-suite"], assemblies: ["Payments"] },
  };
  assert.deepEqual(
    resolveCoverageMode(["--components", '["shared-kernel","payments"]'], selections),
    { mode: "components", names: ["shared-kernel", "payments"] },
  );
  assert.deepEqual(
    resolveCoverageMode(["--components", '["shared-kernel","payments"]'], selections).names.flatMap(
      (name) => selections[name].suites,
    ),
    [...components["shared-kernel"].suites, "payments-suite"],
  );
  assert.throws(() =>
    loadComponents({ ...manifest, schemaVersion: 2 }, { projects, suites, assemblies }),
  );
  assert.throws(() =>
    loadComponents(manifest, { projects: projects.slice(1), suites, assemblies }),
  );
  assert.throws(() => loadComponents(manifest, { projects, suites: suites.slice(1), assemblies }));
  assert.throws(() => resolveCoverageMode(["--components", '["missing"]'], components));
  assert.throws(() =>
    resolveCoverageMode(["--full", "--components", '["shared-kernel"]'], components),
  );
  assert.throws(() => resolveCoverageMode(["--components", "not-json"], components));
  assert.deepEqual(resolveCoverageMode(["--components", "[]"], components), {
    mode: "components",
    names: [],
  });
});
test("isolates disconnected coverage components while retaining every suite in a connected component", () => {
  const disconnected = {
    schemaVersion: 1,
    components: {
      ...manifest.components,
      isolated: {
        projects: [{ path: "src/services/Isolated/Isolated.csproj", assembly: "Isolated" }],
        suites: ["tests/unit/Isolated.UnitTests/Isolated.UnitTests.csproj"],
      },
    },
  };
  const allProjects = [...projects, "src/services/Isolated/Isolated.csproj"];
  const allSuites = [...suites, "tests/unit/Isolated.UnitTests/Isolated.UnitTests.csproj"];
  const allAssemblies = { ...assemblies, "src/services/Isolated/Isolated.csproj": "Isolated" };
  const components = loadComponents(disconnected, {
    projects: allProjects,
    suites: allSuites,
    assemblies: allAssemblies,
  });
  assert.deepEqual(resolveCoverageMode(["--components", '["shared-kernel"]'], components), {
    mode: "components",
    names: ["shared-kernel"],
  });
  assert.deepEqual(components["shared-kernel"].suites, suites);
  assert.equal(components["shared-kernel"].suites.includes(allSuites.at(-1)), false);
  assert.deepEqual(components.isolated.suites, [allSuites.at(-1)]);
});

test("requires identical raw line and branch location-hit sets for equivalent scope runs", () => {
  const report = (lineHits, branchHits) => ({
    "Example.dll": {
      "/repo/src/example.cs": {
        "Example.Type": {
          "Example.Method()": {
            Lines: { 10: lineHits },
            Branches: [
              { Offset: 20, Path: 0, Ordinal: 0, Hits: branchHits },
              { Offset: 20, Path: 1, Ordinal: 1, Hits: 0 },
            ],
          },
        },
      },
    },
  });
  const full = mergeReports([report(1, 0), report(0, 1)]);
  const component = mergeReports([report(0, 1), report(1, 0)]);
  const raw = (modules) => {
    const module = modules.get("Example");
    return {
      lines: Object.fromEntries([...module.lines].sort(([a], [b]) => a.localeCompare(b))),
      branches: Object.fromEntries([...module.branches].sort(([a], [b]) => a.localeCompare(b))),
    };
  };

  assert.deepEqual(raw(full), raw(component));
  assert.notDeepEqual(raw(full), raw(mergeReports([report(1, 0)])));
});
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
test("validates full committed baseline regressions independently of selected assemblies", () => {
  const previous = { Shared: { line: 80, branch: 70 } };
  const current = { Shared: { line: 79, branch: 70 } };
  assert.throws(() => verifyBaseline(current, previous), /coverage regressed/);
  verifyBaseline(previous, previous);
});

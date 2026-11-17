import assert from "node:assert/strict";

export function loadComponents(manifest, { projects, suites, assemblies }) {
  assert.equal(manifest?.schemaVersion, 1, "Coverage components schemaVersion must be 1");
  assert(
    manifest.components && typeof manifest.components === "object",
    "Coverage components must be an object",
  );
  const allProjects = new Set(projects);
  const allSuites = new Set(suites);
  const assignedProjects = new Set();
  const assignedAssemblies = new Set();
  const assignedSuites = new Set();
  const normalized = {};
  for (const [name, component] of Object.entries(manifest.components)) {
    assert(
      name && Array.isArray(component.projects) && component.projects.length,
      `Invalid projects for ${name}`,
    );
    assert(
      Array.isArray(component.suites) && component.suites.length,
      `Invalid suites for ${name}`,
    );
    for (const project of component.projects) {
      assert(allProjects.has(project.path), `Missing measured project ${project.path}`);
      assert(
        assemblies[project.path] === project.assembly,
        `Assembly mismatch for ${project.path}`,
      );
      assert(!assignedProjects.has(project.path), `Duplicate measured project ${project.path}`);
      assert(!assignedAssemblies.has(project.assembly), `Duplicate assembly ${project.assembly}`);
      assignedProjects.add(project.path);
      assignedAssemblies.add(project.assembly);
    }
    for (const suite of component.suites) {
      assert(allSuites.has(suite), `Missing coverage suite ${suite}`);
      assert(!assignedSuites.has(suite), `Suite assigned more than once ${suite}`);
      assignedSuites.add(suite);
    }
    normalized[name] = {
      projects: component.projects.map((p) => p.path),
      assemblies: component.projects.map((p) => p.assembly),
      suites: [...component.suites],
    };
  }
  for (const project of projects)
    assert(assignedProjects.has(project), `Unassigned measured project ${project}`);
  for (const suite of suites)
    assert(assignedSuites.has(suite), `Unassigned coverage suite ${suite}`);
  return normalized;
}

export function resolveCoverageMode(args, components) {
  const full = args.includes("--full");
  const selections = [];
  for (let i = 0; i < args.length; i++) {
    const arg = args[i];
    if (["--full", "--reports-only", "--update-baseline"].includes(arg)) continue;
    if (arg === "--components" || arg === "--components-json") {
      assert(i + 1 < args.length, `${arg} requires a JSON array`);
      let parsed;
      try {
        parsed = JSON.parse(args[++i]);
      } catch (error) {
        throw new Error(`${arg} must be a valid JSON array`, { cause: error });
      }
      assert(
        Array.isArray(parsed) &&
          parsed.every((name) => typeof name === "string" && name.length > 0),
        `${arg} must be a JSON array of component names`,
      );
      selections.push(...parsed);
      continue;
    }
    if (arg.startsWith("--components=") || arg.startsWith("--components-json=")) {
      const option = arg.startsWith("--components-json=") ? "--components-json" : "--components";
      let parsed;
      try {
        parsed = JSON.parse(arg.slice(option.length + 1));
      } catch (error) {
        throw new Error(`${option} must be a valid JSON array`, { cause: error });
      }
      assert(
        Array.isArray(parsed) &&
          parsed.every((name) => typeof name === "string" && name.length > 0),
        `${option} must be a JSON array of component names`,
      );
      selections.push(...parsed);
      continue;
    }
    if (arg === "--component") {
      assert(i + 1 < args.length && args[i + 1], "--component requires a component name");
      selections.push(args[++i]);
      continue;
    }
    if (arg.startsWith("--component=")) {
      selections.push(arg.slice("--component=".length));
      continue;
    }
    assert(false, `Unknown coverage option ${arg}`);
  }
  assert(!(full && selections.length), "Choose --full or component selection, not both");
  if (full) return { mode: "full", names: [] };
  if (selections.length === 0 && args.length === 0) return { mode: "full", names: [] };
  const seen = new Set();
  for (const name of selections) {
    assert(!seen.has(name), `Duplicate coverage component ${name}`);
    seen.add(name);
    assert(components[name], `Unknown coverage component ${name}`);
  }
  return { mode: "components", names: [...seen] };
}

export function mergeReports(reports) {
  const modules = new Map();
  const add = (map, key, hits) => map.set(key, (map.get(key) ?? false) || hits > 0);
  for (const report of reports) {
    for (const [assembly, documents] of Object.entries(report)) {
      const name = assembly.replace(/\.dll$/, "");
      const module = modules.get(name) ?? { lines: new Map(), branches: new Map() };
      modules.set(name, module);
      for (const [document, classes] of Object.entries(documents)) {
        const source = document.replaceAll("\\", "/").split("/src/").at(-1);
        for (const methods of Object.values(classes)) {
          for (const [method, coverage] of Object.entries(methods)) {
            for (const [line, hits] of Object.entries(coverage.Lines))
              add(module.lines, `${source}:${line}`, hits);
            for (const branch of coverage.Branches)
              add(
                module.branches,
                `${source}:${method}:${branch.Offset}:${branch.Path}:${branch.Ordinal}`,
                branch.Hits,
              );
          }
        }
      }
    }
  }
  return modules;
}
export function percentages(modules, assemblies) {
  const percent = (map) =>
    map.size
      ? Math.floor((100_000 * [...map.values()].filter(Boolean).length) / map.size) / 1_000
      : 100;
  return Object.fromEntries(
    assemblies.map((name) => {
      const module = modules.get(name);
      assert(module, `Missing assembly ${name}`);
      return [name, { line: percent(module.lines), branch: percent(module.branches) }];
    }),
  );
}
export function verifyBaseline(actual, baseline) {
  for (const [name, expected] of Object.entries(baseline)) {
    assert(
      actual[name],
      `Coverage scope removed ${name}; migration requires explicit policy review`,
    );
    for (const kind of ["line", "branch"])
      assert(
        Number.isFinite(actual[name][kind]) && actual[name][kind] >= expected[kind],
        `${name} ${kind} coverage regressed: ${actual[name][kind]} < ${expected[kind]}`,
      );
  }
}

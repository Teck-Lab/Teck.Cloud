import assert from "node:assert/strict";

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
      ? Math.floor(([...map.values()].filter(Boolean).length / map.size) * 100_000) / 1000
      : 100;
  return Object.fromEntries(
    assemblies.map((name) => {
      const module = modules.get(name);
      assert(
        module?.lines.size,
        `No instrumented source for ${name}; add tests instead of excluding it`,
      );
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
    for (const kind of ["line", "branch"]) {
      assert(
        Number.isFinite(actual[name][kind]) && actual[name][kind] >= expected[kind],
        `${name} ${kind} coverage regressed: ${actual[name][kind]} < ${expected[kind]}`,
      );
    }
  }
}

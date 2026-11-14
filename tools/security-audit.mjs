import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import fs from "node:fs";

fs.mkdirSync(".artifacts/security", { recursive: true });
const nuget = spawnSync(
  "dotnet",
  [
    "list",
    "Teck.Cloud.slnx",
    "package",
    "--vulnerable",
    "--include-transitive",
    "--format",
    "json",
  ],
  { encoding: "utf8", timeout: 180_000 },
);
assert.ifError(nuget.error);
assert.equal(nuget.status, 0, nuget.stderr);
const report = JSON.parse(nuget.stdout);
fs.writeFileSync(".artifacts/security/nuget.json", JSON.stringify(report, null, 2));
const findings = (report.projects ?? [])
  .flatMap((project) =>
    (project.frameworks ?? []).flatMap((framework) => [
      ...(framework.topLevelPackages ?? []),
      ...(framework.transitivePackages ?? []),
    ]),
  )
  .filter((pkg) => pkg.vulnerabilities?.length);
const bun = spawnSync("bun", ["audit", "--json"], { encoding: "utf8", timeout: 180_000 });
assert.ifError(bun.error);
fs.writeFileSync(".artifacts/security/bun.json", bun.stdout);
fs.writeFileSync(".artifacts/security/bun-stderr.txt", bun.stderr);
assert.equal(bun.status, 0, "Bun dependency audit failed; see .artifacts/security/bun.json");
assert.equal(findings.length, 0, "NuGet vulnerabilities found; see .artifacts/security/nuget.json");
console.log("NuGet (including transitive dependencies) and Bun vulnerability audits passed.");

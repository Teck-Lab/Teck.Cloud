import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import fs from "node:fs";
import {
  summarizeAuditOperationalError,
  summarizeBunFindings,
  summarizeNugetFindings,
} from "./security-audit-summary.mjs";

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
if (nuget.error || nuget.status !== 0) {
  const summary = summarizeAuditOperationalError(
    "nuget",
    nuget.error ?? { code: `exit-${nuget.status}` },
  );
  console.error(JSON.stringify(summary));
  throw nuget.error ?? new Error(`NuGet audit exited ${nuget.status}.`);
}
const report = JSON.parse(nuget.stdout);
fs.writeFileSync(".artifacts/security/nuget.json", JSON.stringify(report, null, 2));
const findings = summarizeNugetFindings(report);
if (findings.length > 0) {
  console.error(JSON.stringify({ ecosystem: "nuget", findingCount: findings.length, findings }));
}
const bun = spawnSync("bun", ["audit", "--json"], { encoding: "utf8", timeout: 180_000 });
fs.writeFileSync(".artifacts/security/bun.json", bun.stdout ?? "");
fs.writeFileSync(".artifacts/security/bun-stderr.txt", bun.stderr ?? "");
if (bun.status !== 0 || bun.error) {
  let bunReport;
  try {
    bunReport = JSON.parse(bun.stdout ?? "");
  } catch {
    bunReport = undefined;
  }
  const summary = bun.error
    ? summarizeAuditOperationalError("bun", bun.error)
    : { ecosystem: "bun", exitCode: bun.status, findings: summarizeBunFindings(bunReport) };
  console.error(JSON.stringify(summary));
  throw bun.error ?? new Error(`Bun dependency audit exited ${bun.status}.`);
}
assert.equal(findings.length, 0, "NuGet vulnerabilities found; see .artifacts/security/nuget.json");
console.log("NuGet (including transitive dependencies) and Bun vulnerability audits passed.");

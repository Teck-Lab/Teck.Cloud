import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import test from "node:test";
import { fileURLToPath } from "node:url";

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const contractRoot = path.join(repoRoot, "docs/contracts/dedicated-postgresql/v1");
const verifier = path.join(repoRoot, "tools/verify-dedicated-postgresql-contract.mjs");

function verify(mutator) {
  const temporary = fs.mkdtempSync(path.join(os.tmpdir(), "teck-contract-"));
  fs.cpSync(contractRoot, temporary, { recursive: true });
  try {
    mutator(temporary);
    return spawnSync(process.execPath, [verifier], {
      cwd: repoRoot,
      env: { ...process.env, TECK_CONTRACT_ROOT: temporary },
      encoding: "utf8",
    });
  } finally {
    fs.rmSync(temporary, { recursive: true, force: true });
  }
}

test("valid contract examples pass schema, route and compatibility checks", () => {
  const result = verify(() => {});
  assert.equal(result.status, 0, result.stderr || result.stdout);
  assert.match(result.stdout, /Validated 13 paths, 14 operations, 6 fixtures/);
});

test("checker rejects tenant-supplied scope in customer order requests", () => {
  const result = verify((directory) => {
    const specPath = path.join(directory, "openapi.yaml");
    const spec = fs
      .readFileSync(specPath, "utf8")
      .replace("additionalProperties: false", "additionalProperties: true");
    fs.writeFileSync(specPath, spec);
  });
  assert.notEqual(result.status, 0);
  assert.match(result.stderr, /tenant scope input/);
});

test("checker rejects wrong progress totals in a fixture", () => {
  const result = verify((directory) => {
    const fixturePath = path.join(directory, "example-migration-progress.json");
    const fixture = JSON.parse(fs.readFileSync(fixturePath, "utf8"));
    fixture.snapshot.failedStepCount = 2;
    fs.writeFileSync(fixturePath, JSON.stringify(fixture));
  });
  assert.notEqual(result.status, 0);
  assert.match(result.stderr, /failure count mismatch/);
});

test("checker rejects SignalR payloads that carry more than an opaque hint", () => {
  const result = verify((directory) => {
    const fixturePath = path.join(directory, "example-stale-overlapping-notifications.json");
    const fixture = JSON.parse(fs.readFileSync(fixturePath, "utf8"));
    fixture.stale.arguments[0].status = "complete";
    fs.writeFileSync(fixturePath, JSON.stringify(fixture));
  });
  assert.notEqual(result.status, 0);
  assert.match(result.stderr, /not allowed/);
});

test("checker rejects examples that imply manual activation", () => {
  const result = verify((directory) => {
    const fixturePath = path.join(directory, "example-manual-exception.json");
    const fixture = JSON.parse(fs.readFileSync(fixturePath, "utf8"));
    fixture.snapshot.manualException.activatedAt = "2026-11-20T12:00:00Z";
    fs.writeFileSync(fixturePath, JSON.stringify(fixture));
  });
  assert.notEqual(result.status, 0);
  assert.match(result.stderr, /does not imply activation authorization/);
});

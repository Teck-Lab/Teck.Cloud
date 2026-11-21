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
test("checker rejects reconnect snapshots fetched before resubscription completion", () => {
  const result = verify((directory) => {
    const fixturePath = path.join(directory, "example-stale-overlapping-notifications.json");
    const fixture = JSON.parse(fs.readFileSync(fixturePath, "utf8"));
    fixture.reconnect.completion.invocationId = "999";
    fs.writeFileSync(fixturePath, JSON.stringify(fixture));
  });
  assert.notEqual(result.status, 0);
  assert.match(result.stderr, /reconnect\.completion\.invocationId/);
});
test("checker rejects void success completions with result or error fields", () => {
  const result = verify((directory) => {
    const fixturePath = path.join(directory, "example-stale-overlapping-notifications.json");
    const fixture = JSON.parse(fs.readFileSync(fixturePath, "utf8"));
    fixture.reconnect.completion.error = "RESOURCE_NOT_VISIBLE";
    fs.writeFileSync(fixturePath, JSON.stringify(fixture));
  });
  assert.notEqual(result.status, 0);
  assert.match(result.stderr, /Expected values to be strictly equal/);
});
test("checker rejects SignalR schema without normative failure recovery text", () => {
  const result = verify((directory) => {
    const schemaPath = path.join(directory, "signalr-events.schema.json");
    const schema = JSON.parse(fs.readFileSync(schemaPath, "utf8"));
    schema.description = "OperationChanged notifications are hints only.";
    fs.writeFileSync(schemaPath, JSON.stringify(schema));
  });
  assert.notEqual(result.status, 0);
  assert.match(result.stderr, /normatively define subscription failure and snapshot recovery/);
});

test("checker rejects unknown subscription failure codes", () => {
  const result = verify((directory) => {
    const fixturePath = path.join(directory, "example-stale-overlapping-notifications.json");
    const fixture = JSON.parse(fs.readFileSync(fixturePath, "utf8"));
    fixture.subscriptionFailureExamples.unavailable.error = "INTERNAL_ERROR";
    fs.writeFileSync(fixturePath, JSON.stringify(fixture));
  });
  assert.notEqual(result.status, 0);
  assert.match(
    result.stderr,
    /subscriptionFailureExamples|must equal|oneOf branch|Expected values to be strictly equal/,
  );
});

test("checker rejects subscription failures with result payloads", () => {
  const result = verify((directory) => {
    const fixturePath = path.join(directory, "example-stale-overlapping-notifications.json");
    const fixture = JSON.parse(fs.readFileSync(fixturePath, "utf8"));
    fixture.subscriptionFailureExamples.notVisible.result = null;
    fs.writeFileSync(fixturePath, JSON.stringify(fixture));
  });
  assert.notEqual(result.status, 0);
  assert.match(
    result.stderr,
    /subscriptionFailureExamples|additional properties|oneOf branch|must not include a result/,
  );
});

test("checker rejects client subscription requests that supply tenant or group scope", () => {
  const result = verify((directory) => {
    const schemaPath = path.join(directory, "realtime-subscription.schema.json");
    const schema = JSON.parse(fs.readFileSync(schemaPath, "utf8"));
    schema.$defs.subscribeToOrder.properties.arguments.prefixItems[0].properties.tenantId = {
      type: "string",
    };
    fs.writeFileSync(schemaPath, JSON.stringify(schema));
  });
  assert.notEqual(result.status, 0);
  assert.match(result.stderr, /tenantId/);
});

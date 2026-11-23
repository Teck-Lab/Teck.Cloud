import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import test from "node:test";
import { fileURLToPath } from "node:url";

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const contractRoot = path.join(repoRoot, "docs/contracts/commerce/v1");
const verifier = path.join(repoRoot, "tools/verify-commerce-contract.mjs");

function copyContract() {
  const temporary = fs.mkdtempSync(path.join(os.tmpdir(), "teck-commerce-contract-"));
  fs.cpSync(contractRoot, temporary, {
    recursive: true,
    filter: (source) => !/[\\/](bin|obj)([\\/]|$)/u.test(source),
  });
  return temporary;
}

function runVerifier(root) {
  return spawnSync(process.execPath, [verifier], {
    encoding: "utf8",
    env: { ...process.env, TECK_COMMERCE_CONTRACT_ROOT: root },
    timeout: 120_000,
  });
}

function expectRejected(mutate, label) {
  const temporary = copyContract();
  try {
    mutate(temporary);
    const result = runVerifier(temporary);
    assert.notEqual(
      result.status,
      0,
      `${label} must be rejected\n${result.stdout}${result.stderr}`,
    );
  } finally {
    fs.rmSync(temporary, { recursive: true, force: true });
  }
}

function mutateJson(directory, file, mutate) {
  const target = path.join(directory, file);
  const value = JSON.parse(fs.readFileSync(target, "utf8"));
  mutate(value);
  fs.writeFileSync(target, `${JSON.stringify(value, null, 2)}\n`);
}

function mutateText(directory, file, from, to) {
  const target = path.join(directory, file);
  const text = fs.readFileSync(target, "utf8");
  assert.ok(text.includes(from), `mutation anchor exists in ${file}: ${from}`);
  fs.writeFileSync(target, text.replace(from, to));
}

test("valid commerce contract passes schema, example, lifecycle and protobuf checks", () => {
  const result = runVerifier(contractRoot);
  assert.equal(result.status, 0, `${result.stdout}${result.stderr}`);
  assert.match(result.stdout, /Validated \d+ operations/u);
});

test("checker rejects an invalid tenant UUID in a durable event", () => {
  expectRejected(
    (directory) =>
      mutateJson(directory, "example-license-desired.json", (fixture) => {
        fixture.event.tenantId = "not-a-uuid";
      }),
    "invalid tenant UUID",
  );
});

test("checker rejects an unknown customer order status", () => {
  expectRejected(
    (directory) =>
      mutateJson(directory, "example-order-payment-pending.json", (fixture) => {
        fixture.order.status = "futureState";
      }),
    "unknown order status",
  );
});

test("checker rejects staff-only fields exposed in a customer example", () => {
  expectRejected(
    (directory) =>
      mutateJson(directory, "example-order-shared-active.json", (fixture) => {
        fixture.order.payment = { status: "paid", paidThrough: null };
      }),
    "staff-only customer field",
  );
});

test("checker rejects a wrong required field type in an example", () => {
  expectRejected(
    (directory) =>
      mutateJson(directory, "example-order-payment-pending.json", (fixture) => {
        fixture.order.total.amountMinor = "1000";
      }),
    "invalid money type",
  );
});

test("checker rejects a missing required fixture", () => {
  expectRejected(
    (directory) => fs.rmSync(path.join(directory, "example-problems.json")),
    "missing required fixture",
  );
});

test("checker rejects an unknown license rejection code", () => {
  expectRejected(
    (directory) =>
      mutateJson(directory, "example-license-verification.json", (fixture) => {
        fixture.rejections[1].rejection = "unknown_reason";
      }),
    "unknown license rejection",
  );
});

test("checker rejects protobuf field numbering out of schema order", () => {
  expectRejected(
    (directory) =>
      mutateText(
        directory,
        "orders.proto",
        "  uint32 desired_revision = 4;",
        "  uint32 desired_revision = 5;",
      ),
    "protobuf field numbering",
  );
});

test("checker rejects a missing internal entitlement RPC", () => {
  expectRejected(
    (directory) =>
      mutateText(
        directory,
        "orders.proto",
        "  rpc CheckCurrentEntitlement(CheckCurrentEntitlementRequest)\n      returns (CheckCurrentEntitlementResponse);\n",
        "",
      ),
    "missing CheckCurrentEntitlement RPC",
  );
});

test("checker rejects a delete operation on the customer gateway", () => {
  expectRejected(
    (directory) =>
      mutateText(
        directory,
        "customer.openapi.yaml",
        "  /v1/commerce/orders/{orderId}:\n    get:",
        '  /v1/commerce/orders/{orderId}:\n    delete:\n      operationId: deleteCommerceOrder\n      tags: [Customer orders]\n      x-required-permission: commerce.customer\n      responses:\n        "200":\n          description: Deleted\n    get:',
      ),
    "forbidden delete operation",
  );
});

test("protobuf contract compiles with locked restore", () => {
  const version = spawnSync("dotnet", ["--version"], { encoding: "utf8", timeout: 60_000 });
  assert.equal(
    version.error,
    undefined,
    "dotnet SDK is required to compile the contract protos; install the global.json SDK",
  );
  assert.equal(version.status, 0, version.stderr);
  assert.ok(
    fs.existsSync(path.join(contractRoot, "packages.lock.json")),
    "the protobuf harness must keep a checked-in package lock",
  );
  const build = spawnSync(
    "dotnet",
    [
      "build",
      path.join(contractRoot, "Commerce.Contract.Protos.csproj"),
      "-p:RestoreLockedMode=true",
      "-nologo",
      "-v",
      "minimal",
    ],
    {
      cwd: repoRoot,
      encoding: "utf8",
      timeout: 600_000,
      maxBuffer: 32 * 1024 * 1024,
      env: { ...process.env, DOTNET_NOLOGO: "true", DOTNET_CLI_TELEMETRY_OPTOUT: "1" },
    },
  );
  assert.equal(build.status, 0, `protobuf compilation failed\n${build.stdout}${build.stderr}`);
});

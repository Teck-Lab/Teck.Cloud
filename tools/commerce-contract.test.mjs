import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import test from "node:test";
import { fileURLToPath } from "node:url";
import { parse, stringify } from "yaml";

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

function expectRejected(mutate, label, expectedDiagnostic) {
  const temporary = copyContract();
  try {
    mutate(temporary);
    const result = runVerifier(temporary);
    assert.equal(result.error, undefined, `${label} verifier process starts`);
    assert.equal(result.signal, null, `${label} verifier process is not signalled`);
    assert.equal(result.status, 1, `${label} exits with a contract assertion failure`);
    assert.match(result.stderr, expectedDiagnostic, `${label} reports its semantic diagnostic`);
    assert.doesNotMatch(result.stderr, /ERR_MODULE_NOT_FOUND/u);
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

function mutateOpenApiExample(directory, file, exampleName, mutate) {
  const target = path.join(directory, file);
  const document = parse(fs.readFileSync(target, "utf8"));
  const example = document.components.examples[exampleName];
  assert.ok(example, `example mutation anchor exists: ${file} ${exampleName}`);
  mutate(example.value);
  fs.writeFileSync(target, stringify(document));
}

test("checker rejects an invalid Admin-only OpenAPI response example", () => {
  expectRejected(
    (directory) =>
      mutateOpenApiExample(directory, "admin.openapi.yaml", "AdminOrderVerifying", (order) => {
        order.stripeInvoiceId = "in_test_private";
      }),
    "Admin OpenAPI privacy",
    /admin mutation response AdminOrderVerifying\.stripeInvoiceId is forbidden/u,
  );
});

test("checker rejects an invalid dedicated checkout OpenAPI example total", () => {
  expectRejected(
    (directory) =>
      mutateOpenApiExample(
        directory,
        "customer.openapi.yaml",
        "CreatedDedicatedCheckout",
        (response) => {
          response.order.total.amountMinor = 3501;
        },
      ),
    "OpenAPI-only dedicated checkout total",
    /customer OpenAPI example CreatedDedicatedCheckout\.order order total/u,
  );
});

test("checker rejects a pending readiness Admin snapshot with a pending payment", () => {
  expectRejected(
    (directory) => {
      mutateJson(directory, "example-admin-order-grant-pending.json", (fixture) => {
        fixture.order.payment.status = "pending";
        fixture.order.payment.paidThrough = null;
      });
      mutateOpenApiExample(directory, "admin.openapi.yaml", "AdminOrderGrantPending", (order) => {
        order.payment.status = "pending";
        order.payment.paidThrough = null;
      });
    },
    "pending readiness paid gate",
    /example-admin-order-grant-pending\.json pending readiness gate paid/u,
  );
});

test("checker rejects a final rejection hidden by pending payment", () => {
  expectRejected(
    (directory) => {
      mutateJson(directory, "example-admin-order-rejected.json", (fixture) => {
        fixture.order.status = "pendingPayment";
        fixture.order.payment.status = "pending";
        fixture.order.payment.paidThrough = null;
      });
      mutateOpenApiExample(directory, "admin.openapi.yaml", "AdminOrderRejected", (order) => {
        order.status = "pendingPayment";
        order.payment.status = "pending";
        order.payment.paidThrough = null;
      });
    },
    "rejected status precedence",
    /example-admin-order-rejected\.json rejection is final/u,
  );
});

test("checker rejects immutable order facts changed across projection instants", () => {
  expectRejected(
    (directory) => {
      mutateJson(directory, "example-order-shared-active.json", (fixture) => {
        fixture.order.createdAt = "2026-11-23T18:59:00Z";
      });
      mutateOpenApiExample(directory, "customer.openapi.yaml", "SharedActiveOrder", (order) => {
        order.createdAt = "2026-11-23T18:59:00Z";
      });
    },
    "immutable order creation instant",
    /immutable createdAt binding/u,
  );
});

test("checker rejects desiredRevision in either public order schema", () => {
  for (const [filename, schemaName] of [
    ["customer.openapi.yaml", "CustomerOrder"],
    ["admin.openapi.yaml", "AdminOrder"],
  ])
    expectRejected(
      (directory) => {
        const target = path.join(directory, filename);
        const document = parse(fs.readFileSync(target, "utf8"));
        document.components.schemas[schemaName].properties.desiredRevision = {
          type: "integer",
          minimum: 1,
        };
        fs.writeFileSync(target, stringify(document));
      },
      `${schemaName} internal generation schema privacy`,
      new RegExp(`${schemaName} hides desiredRevision`, "u"),
    );
});

test("checker accepts grant-pending replay with the matching first-create intent", () => {
  const temporary = copyContract();
  try {
    mutateJson(temporary, "example-checkout-replay-paid.json", (fixture) => {
      fixture.response.order.status = "pendingReadiness";
      fixture.response.order.revision = 5;
      fixture.response.order.updatedAt = "2026-11-23T20:16:00Z";
      fixture.response.order.license = null;
    });
    mutateOpenApiExample(temporary, "customer.openapi.yaml", "PaidCheckoutReplay", (response) => {
      response.order.status = "pendingReadiness";
      response.order.revision = 5;
      response.order.updatedAt = "2026-11-23T20:16:00Z";
      response.order.license = null;
    });
    const result = runVerifier(temporary);
    assert.equal(result.status, 0, `${result.stdout}${result.stderr}`);
  } finally {
    fs.rmSync(temporary, { recursive: true, force: true });
  }
});

test("checker rejects grant-pending replay without a matching first-create intent", () => {
  expectRejected(
    (directory) => {
      const grantPending = JSON.parse(
        fs.readFileSync(path.join(directory, "example-order-grant-pending.json"), "utf8"),
      ).order;
      mutateJson(directory, "example-checkout-replay-paid.json", (fixture) => {
        fixture.response.order = grantPending;
        fixture.response.checkoutUrl = null;
      });
      mutateOpenApiExample(directory, "customer.openapi.yaml", "PaidCheckoutReplay", (response) => {
        response.order = grantPending;
        response.checkoutUrl = null;
      });
    },
    "grant-pending replay first-create binding",
    /shared replay order binding/u,
  );
});

for (const [label, file, exampleName, mutateCustomer] of [
  ["detail", "example-order-shared-active.json", "SharedActiveOrder", (value) => value.order],
  ["page", "example-orders-page.json", "OrdersPage", (value) => value.page.items[0]],
  [
    "replay",
    "example-checkout-replay-paid.json",
    "PaidCheckoutReplay",
    (value) => value.response.order,
  ],
])
  test(`checker rejects an unclamped active ${label} license expiry`, () => {
    expectRejected(
      (directory) => {
        mutateJson(directory, file, (fixture) => {
          mutateCustomer(fixture).license.expiresAt = "2026-12-23T20:00:00Z";
        });
        mutateOpenApiExample(directory, "customer.openapi.yaml", exampleName, (value) => {
          const order = exampleName === "OrdersPage" ? value.items[0] : (value.order ?? value);
          order.license.expiresAt = "2026-12-23T20:00:00Z";
        });
      },
      `active ${label} license expiry clamp`,
      /order license expiry clamp/u,
    );
  });

test("checker rejects an active Admin order with stale ready evidence", () => {
  expectRejected(
    (directory) => {
      mutateJson(directory, "example-admin-order-ready.json", (fixture) => {
        fixture.order.fulfillment.observedAt = "2026-11-23T19:30:00Z";
      });
      mutateOpenApiExample(directory, "admin.openapi.yaml", "AdminOrderReady", (order) => {
        order.fulfillment.observedAt = "2026-11-23T19:30:00Z";
      });
    },
    "active Admin stale ready proof",
    /ready proof is fresh/u,
  );
});

test("checker rejects a shared page total mismatch", () => {
  expectRejected(
    (directory) => {
      mutateJson(directory, "example-orders-page.json", (fixture) => {
        fixture.page.items[0].total.amountMinor = 1001;
      });
      mutateOpenApiExample(directory, "customer.openapi.yaml", "OrdersPage", (page) => {
        page.items[0].total.amountMinor = 1001;
      });
    },
    "customer page item total",
    /example-orders-page\.json\.items\[0\] order total/u,
  );
});

test("checker rejects an active page item without a license", () => {
  expectRejected(
    (directory) => {
      mutateJson(directory, "example-orders-page.json", (fixture) => {
        fixture.page.items[0].license = null;
      });
      mutateOpenApiExample(directory, "customer.openapi.yaml", "OrdersPage", (page) => {
        page.items[0].license = null;
      });
    },
    "active page customer license",
    /example-orders-page\.json\.items\[0\] active license/u,
  );
});

test("checker rejects invalid Admin active gate facts", () => {
  expectRejected(
    (directory) => {
      mutateJson(directory, "example-admin-order-ready.json", (fixture) => {
        fixture.order.payment.status = "pending";
        fixture.order.payment.paidThrough = null;
      });
      mutateOpenApiExample(directory, "admin.openapi.yaml", "AdminOrderReady", (order) => {
        order.payment.status = "pending";
        order.payment.paidThrough = null;
      });
    },
    "active Admin payment gate",
    /example-admin-order-ready\.json active gate paid/u,
  );
});

test("checker rejects terminal probe_unavailable on an Admin page item", () => {
  expectRejected(
    (directory) => {
      mutateJson(directory, "example-admin-orders-page.json", (fixture) => {
        const order = fixture.page.items[0];
        order.status = "failed";
        order.fulfillment = {
          status: "failed",
          placementId: "5d6e7f80-91a2-4c3d-9e4f-506172839405",
          evidenceReference: "evidence://ops/2026-11-23/dedicated-primary-check",
          observedAt: order.updatedAt,
          failureCode: "probe_unavailable",
        };
      });
      mutateOpenApiExample(directory, "admin.openapi.yaml", "AdminOrdersPage", (page) => {
        const order = page.items[0];
        order.status = "failed";
        order.fulfillment = {
          status: "failed",
          placementId: "5d6e7f80-91a2-4c3d-9e4f-506172839405",
          evidenceReference: "evidence://ops/2026-11-23/dedicated-primary-check",
          observedAt: order.updatedAt,
          failureCode: "probe_unavailable",
        };
      });
    },
    "terminal page probe failure",
    /terminal fulfillment failure code/u,
  );
});

test("checker rejects a retryable probe failure marked terminal", () => {
  expectRejected(
    (directory) => {
      mutateJson(directory, "example-admin-order-failed.json", (fixture) => {
        fixture.order.fulfillment.failureCode = "probe_unavailable";
      });
      mutateOpenApiExample(directory, "admin.openapi.yaml", "AdminOrderFailed", (order) => {
        order.fulfillment.failureCode = "probe_unavailable";
      });
    },
    "terminal probe failure",
    /terminal fulfillment failure code/u,
  );
});

test("checker rejects a desired event tenant mismatch", () => {
  expectRejected(
    (directory) =>
      mutateJson(directory, "example-license-desired.json", (fixture) => {
        fixture.event.tenantId = "8f0e0d0c-0b0a-4999-8877-665544332211";
      }),
    "desired outcome tenant binding",
    /desired outcome tenant binding|license claims tenant binding/u,
  );
});

test("checker rejects desired/outcome generation mismatch", () => {
  expectRejected(
    (directory) =>
      mutateJson(directory, "example-license-desired.json", (fixture) => {
        fixture.event.revision = 8;
      }),
    "desired outcome generation",
    /desired outcome generation/u,
  );
});

test("checker rejects desired/outcome expiry mismatch", () => {
  expectRejected(
    (directory) =>
      mutateJson(directory, "example-license-desired.json", (fixture) => {
        fixture.event.notAfter = "2026-11-23T20:31:00Z";
      }),
    "desired outcome expiry",
    /desired outcome expiry/u,
  );
});

test("checker rejects verification claims with the wrong key", () => {
  expectRejected(
    (directory) =>
      mutateJson(directory, "example-license-verification.json", (fixture) => {
        fixture.valid.claims.keyId = "K2";
      }),
    "license claims key binding",
    /license claims key binding/u,
  );
});

test("checker rejects paid replay with a different order binding", () => {
  expectRejected(
    (directory) => {
      mutateJson(directory, "example-checkout-replay-paid.json", (fixture) => {
        fixture.response.order.orderId = "8f0e0d0c-0b0a-4999-8877-665544332211";
      });
      mutateOpenApiExample(directory, "customer.openapi.yaml", "PaidCheckoutReplay", (response) => {
        response.order.orderId = "8f0e0d0c-0b0a-4999-8877-665544332211";
      });
    },
    "paid replay order binding",
    /shared replay order binding/u,
  );
});

test("checker rejects an active replay with missing Admin generation proof", () => {
  expectRejected(
    (directory) => {
      mutateJson(directory, "example-checkout-replay-paid.json", (fixture) => {
        fixture.response.order.orderId = "8f0e0d0c-0b0a-4999-8877-665544332211";
        fixture.response.order.subscriptionId = "9f0e0d0c-0b0a-4999-8877-665544332211";
      });
      mutateOpenApiExample(directory, "customer.openapi.yaml", "PaidCheckoutReplay", (response) => {
        response.order.orderId = "8f0e0d0c-0b0a-4999-8877-665544332211";
        response.order.subscriptionId = "9f0e0d0c-0b0a-4999-8877-665544332211";
      });
      mutateJson(directory, "example-admin-order-shared-ready.json", (fixture) => {
        fixture.order.orderId = "8f0e0d0c-0b0a-4999-8877-665544332211";
        fixture.order.subscriptionId = "9f0e0d0c-0b0a-4999-8877-665544332211";
      });
      mutateOpenApiExample(directory, "admin.openapi.yaml", "AdminOrderSharedReady", (order) => {
        order.orderId = "8f0e0d0c-0b0a-4999-8877-665544332211";
        order.subscriptionId = "9f0e0d0c-0b0a-4999-8877-665544332211";
      });
    },
    "active replay order binding precedes Admin evidence join",
    /shared replay order binding/u,
  );
});

test("checker rejects a missing newly required fixture", () => {
  expectRejected(
    (directory) => fs.rmSync(path.join(directory, "example-basket-cleared.json")),
    "missing cleared basket fixture",
    /missing required fixture example-basket-cleared\.json/u,
  );
});

test("checker rejects protobuf desired_revision presence being removed", () => {
  expectRejected(
    (directory) =>
      mutateText(
        directory,
        "orders.proto",
        "  optional uint32 desired_revision = 11;",
        "  uint32 desired_revision = 11;",
      ),
    "desired revision optional presence",
    /Expected values to be strictly deep-equal/u,
  );
});

test("checker rejects a customer example that leaks the internal generation", () => {
  expectRejected(
    (directory) => {
      mutateJson(directory, "example-order-shared-active.json", (fixture) => {
        fixture.order.desiredRevision = 7;
      });
      mutateOpenApiExample(directory, "customer.openapi.yaml", "SharedActiveOrder", (order) => {
        order.desiredRevision = 7;
      });
    },
    "customer desired revision privacy",
    /desiredRevision is forbidden in this projection/u,
  );
});

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
    /tenantId must be a lowercase UUID/u,
  );
});

test("checker rejects an unknown customer order status", () => {
  expectRejected(
    (directory) =>
      mutateJson(directory, "example-order-payment-pending.json", (fixture) => {
        fixture.order.status = "futureState";
      }),
    "unknown order status",
    /status must be one of/u,
  );
});

test("checker rejects staff-only fields exposed in a customer example", () => {
  expectRejected(
    (directory) =>
      mutateJson(directory, "example-order-shared-active.json", (fixture) => {
        fixture.order.payment = { status: "paid", paidThrough: null };
      }),
    "staff-only customer field",
    /\.payment is forbidden/u,
  );
});

test("checker rejects a wrong required field type in an example", () => {
  expectRejected(
    (directory) =>
      mutateJson(directory, "example-order-payment-pending.json", (fixture) => {
        fixture.order.total.amountMinor = "1000";
      }),
    "invalid money type",
    /amountMinor must be integer/u,
  );
});

test("checker rejects a missing required fixture", () => {
  expectRejected(
    (directory) => fs.rmSync(path.join(directory, "example-problems.json")),
    "missing required fixture",
    /ENOENT.*example-problems\.json/su,
  );
});

test("checker rejects an unknown license rejection code", () => {
  expectRejected(
    (directory) =>
      mutateJson(directory, "example-license-verification.json", (fixture) => {
        fixture.rejections.find((rejection) => rejection.rejection === "expired").rejection =
          "unknown_reason";
      }),
    "unknown license rejection",
    /rejections\[1\]\.rejection has unsupported code unknown_reason/u,
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
    /CheckCurrentEntitlementRequest\.desired_revision must use field number 4/u,
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
    /CheckCurrentEntitlement/u,
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
    /customer forbids delete on \/v1\/commerce\/orders\/\{orderId\}/u,
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

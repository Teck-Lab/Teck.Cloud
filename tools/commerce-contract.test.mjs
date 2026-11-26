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
const SHARED_ACTIVE_ID = "0f0e0d0c-0b0a-4999-8877-665544332211";
const DEDICATED_APPROVED_ID = "2b3c4d5e-6f70-4a1b-9c2d-3e4f50617283";
const GRANT_PENDING_ID = "9c0d1e2f-3a4b-4c5d-8e9f-001122334455";
const CUSTOMER_PAGE = ["example-orders-page.json", "customer.openapi.yaml", "OrdersPage"];
const ADMIN_PAGE = ["example-admin-orders-page.json", "admin.openapi.yaml", "AdminOrdersPage"];

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
    assert.doesNotMatch(
      result.stderr,
      /ERR_MODULE_NOT_FOUND|matches OpenAPI example|fixture parity/u,
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

function mutateOpenApi(directory, file, mutate) {
  const target = path.join(directory, file);
  const document = parse(fs.readFileSync(target, "utf8"));
  mutate(document);
  fs.writeFileSync(target, stringify(document));
}

function mutateOpenApiExample(directory, file, exampleName, mutate) {
  mutateOpenApi(directory, file, (document) => {
    const example = document.components.examples[exampleName];
    assert.ok(example, `example mutation anchor exists: ${file} ${exampleName}`);
    mutate(example.value);
  });
}

function mutatePage(directory, [file, openApi, component], mutate) {
  mutateJson(directory, file, ({ page }) => mutate(page.items));
  mutateOpenApiExample(directory, openApi, component, ({ items }) => mutate(items));
}

function pageOrder(items, orderId) {
  const order = items.find((item) => item.orderId === orderId);
  assert.ok(order, `page fixture contains ${orderId}`);
  return order;
}

function mutatePaidReplay(directory, mutate) {
  mutateJson(directory, "example-checkout-replay-paid.json", ({ response }) => mutate(response));
  mutateOpenApiExample(directory, "customer.openapi.yaml", "PaidCheckoutReplay", mutate);
}

function readFixture(directory, file) {
  return JSON.parse(fs.readFileSync(path.join(directory, file), "utf8"));
}

function setGrantPendingReplay(directory) {
  mutatePaidReplay(directory, (response) => {
    Object.assign(response.order, {
      status: "pendingReadiness",
      revision: 3,
      updatedAt: "2026-11-23T20:14:00Z",
      license: null,
    });
    response.checkoutUrl = null;
  });
}

function insertSharedAdminSnapshot(directory, mutate) {
  const { order } = readFixture(directory, "example-admin-order-shared-ready.json");
  mutate(order);
  mutatePage(directory, ADMIN_PAGE, (items) => {
    assert.ok(!items.some((item) => item.orderId === order.orderId));
    const index = items.findIndex((item) => item.orderId === DEDICATED_APPROVED_ID);
    assert.ok(index >= 0);
    items.splice(index, 0, order);
  });
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

test("checker accepts grant-pending replay with exact same-intent Admin evidence", () => {
  const temporary = copyContract();
  try {
    setGrantPendingReplay(temporary);
    const { response: replay } = readFixture(temporary, "example-checkout-replay-paid.json");
    const { response: created } = readFixture(temporary, "example-checkout-created-shared.json");
    assert.equal(replay.order.orderId, created.order.orderId);
    assert.equal(replay.order.subscriptionId, created.order.subscriptionId);
    assert.equal(replay.order.license, null);
    assert.equal(replay.checkoutUrl, null);
    insertSharedAdminSnapshot(temporary, (order) => {
      order.status = replay.order.status;
      order.revision = replay.order.revision;
      order.updatedAt = replay.order.updatedAt;
      order.fulfillment.observedAt = replay.order.updatedAt;
    });
    const result = runVerifier(temporary);
    assert.equal(result.status, 0, `${result.stdout}${result.stderr}`);
  } finally {
    fs.rmSync(temporary, { recursive: true, force: true });
  }
});

test("checker rejects grant-pending replay without exact same-intent Admin evidence", () => {
  expectRejected(
    setGrantPendingReplay,
    "grant-pending replay Admin evidence",
    /example-checkout-replay-paid\.json\.order requires same-snapshot Admin ready grant-pending evidence/u,
  );
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
  [
    "page",
    "example-orders-page.json",
    "OrdersPage",
    (value) => pageOrder(value.page.items, SHARED_ACTIVE_ID),
  ],
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
          const order =
            exampleName === "OrdersPage"
              ? pageOrder(value.items, SHARED_ACTIVE_ID)
              : (value.order ?? value);
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
    (directory) =>
      mutatePage(directory, CUSTOMER_PAGE, (items) => {
        pageOrder(items, SHARED_ACTIVE_ID).total.amountMinor = 1001;
      }),
    "customer page item total",
    /example-orders-page\.json\.items\[\d+\] order total/u,
  );
});

test("checker rejects an active page item without a license", () => {
  expectRejected(
    (directory) =>
      mutatePage(directory, CUSTOMER_PAGE, (items) => {
        pageOrder(items, SHARED_ACTIVE_ID).license = null;
      }),
    "active page customer license",
    /example-orders-page\.json\.items\[\d+\] active license/u,
  );
});

for (const [gate, mutate] of [
  ["paid", (order) => Object.assign(order.payment, { status: "pending", paidThrough: null })],
  [
    "approved",
    (order) =>
      Object.assign(order.approval, {
        status: "pending",
        actorId: null,
        reason: null,
        occurredAt: null,
      }),
  ],
  ["ready", (order) => Object.assign(order.fulfillment, { status: "verifying", observedAt: null })],
]) {
  test(`checker rejects an active Admin order without the ${gate} gate`, () => {
    expectRejected(
      (directory) => {
        mutateJson(directory, "example-admin-order-ready.json", ({ order }) => mutate(order));
        mutateOpenApiExample(directory, "admin.openapi.yaml", "AdminOrderReady", mutate);
      },
      `active Admin ${gate} gate`,
      new RegExp(`example-admin-order-ready\\.json active gate ${gate}`, "u"),
    );
  });
}

test("checker rejects an Admin page total mismatch", () => {
  expectRejected(
    (directory) =>
      mutatePage(directory, ADMIN_PAGE, (items) => {
        pageOrder(items, DEDICATED_APPROVED_ID).total.amountMinor = 3501;
      }),
    "Admin page item total",
    /example-admin-orders-page\.json\.items\[\d+\] order total/u,
  );
});

test("checker rejects grant-pending evidence exactly at its freshness limit", () => {
  expectRejected(
    (directory) => {
      const expireProof = (order) => {
        order.fulfillment.observedAt = "2026-11-23T20:01:00Z";
      };
      mutateJson(directory, "example-admin-order-grant-pending.json", ({ order }) =>
        expireProof(order),
      );
      mutateOpenApiExample(directory, "admin.openapi.yaml", "AdminOrderGrantPending", expireProof);
    },
    "grant-pending Admin freshness boundary",
    /example-admin-order-grant-pending\.json ready proof is fresh/u,
  );
});

test("checker rejects terminal probe_unavailable on an Admin page item", () => {
  expectRejected(
    (directory) =>
      mutatePage(directory, ADMIN_PAGE, (items) => {
        const order = pageOrder(items, DEDICATED_APPROVED_ID);
        order.status = "failed";
        order.fulfillment = {
          status: "failed",
          placementId: "5d6e7f80-91a2-4c3d-9e4f-506172839405",
          evidenceReference: "evidence://ops/2026-11-23/dedicated-primary-check",
          observedAt: order.updatedAt,
          failureCode: "probe_unavailable",
        };
      }),
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

test("checker rejects verification claims for a different tenant", () => {
  expectRejected(
    (directory) =>
      mutateJson(directory, "example-license-verification.json", (fixture) => {
        fixture.valid.claims.tenantId = "8f0e0d0c-0b0a-4999-8877-665544332211";
      }),
    "license claims tenant binding",
    /license claims tenant binding/u,
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

test("checker rejects an active replay without exact same-intent Admin evidence", () => {
  expectRejected(
    (directory) =>
      mutatePaidReplay(directory, ({ order }) => {
        order.revision = 5;
        order.updatedAt = "2026-11-23T20:17:00Z";
      }),
    "active replay Admin evidence",
    /example-checkout-replay-paid\.json\.order requires same-snapshot Admin active grant/u,
  );
});

test("checker rejects conflicting Admin fulfillment at the active replay instant", () => {
  expectRejected(
    (directory) =>
      insertSharedAdminSnapshot(directory, (order) => {
        order.fulfillment.evidenceReference = "evidence://ops/2026-11-23/shared-other-check";
      }),
    "conflicting exact-instant Admin fulfillment",
    /same-snapshot fulfillment agreement \(evidenceReference\)/u,
  );
});

for (const file of [
  "example-basket-cleared.json",
  "example-checkout-replay-paid.json",
  "example-order-grant-pending.json",
  "example-order-probe-retrying.json",
  "example-admin-order-grant-pending.json",
  "example-admin-order-probe-retrying.json",
  "example-admin-order-shared-ready.json",
]) {
  test(`checker rejects missing ${file}`, () => {
    expectRejected(
      (directory) => fs.rmSync(path.join(directory, file)),
      `missing ${file}`,
      new RegExp(`missing required fixture ${RegExp.escape(file)}`, "u"),
    );
  });
}

for (const destination of ["removed", "200 JSON", "201 problem+json"]) {
  test(`checker rejects a paid replay mount ${destination}`, () => {
    expectRejected(
      (directory) =>
        mutateOpenApi(directory, "customer.openapi.yaml", ({ paths }) => {
          const responses = paths["/v1/commerce/orders"].post.responses;
          const content = responses["201"].content;
          const json = content["application/json"];
          const { paidReplay } = json.examples;
          delete json.examples.paidReplay;
          const media = { schema: json.schema, examples: { paidReplay } };
          if (destination === "200 JSON") {
            responses["200"] = {
              description: "Misplaced replay",
              content: { "application/json": media },
            };
          } else if (destination === "201 problem+json") {
            content["application/problem+json"] = media;
          }
        }),
      `checkout paid replay mount ${destination}`,
      /createCommerceOrder 201 application\/json paidReplay must reference PaidCheckoutReplay/u,
    );
  });
}

const ADMIN_ORDER_PATH = "/v1/admin/commerce/tenants/{tenantId}/orders/{orderId}";
function adminMutationExamples(paths, route) {
  return paths[`${ADMIN_ORDER_PATH}/${route}`].post.responses["200"].content["application/json"]
    .examples;
}

test("checker rejects evidence response mounted on the decision instead", () => {
  expectRejected(
    (directory) =>
      mutateOpenApi(directory, "admin.openapi.yaml", ({ paths }) => {
        const evidence = adminMutationExamples(paths, "placement-evidence");
        adminMutationExamples(paths, "decision").verifying = evidence.verifying;
        delete evidence.verifying;
      }),
    "misplaced Admin verifying mount",
    /recordAdminPlacementEvidence 200 application\/json verifying must reference AdminOrderVerifying/u,
  );
});

test("checker requires decision approved mount even with the detail approved mount", () => {
  expectRejected(
    (directory) =>
      mutateOpenApi(directory, "admin.openapi.yaml", ({ paths }) => {
        delete adminMutationExamples(paths, "decision").approved;
      }),
    "missing Admin approved decision mount",
    /decideAdminCommerceOrder 200 application\/json approved must reference AdminOrderApproved/u,
  );
});

for (const page of [CUSTOMER_PAGE, ADMIN_PAGE]) {
  for (const key of ["updatedAt", "orderId tie-breaker"]) {
    test(`checker rejects ${page[2]} with inverted ${key} order`, () => {
      expectRejected(
        (directory) =>
          mutatePage(directory, page, (items) => {
            const [latest, next] = items;
            assert.equal(next.orderId, GRANT_PENDING_ID);
            if (key === "orderId tie-breaker") {
              assert.ok(latest.orderId < next.orderId);
              next.updatedAt = latest.updatedAt;
              // Keep the Admin ready proof fresh at the retimed page snapshot.
              if (page === ADMIN_PAGE) next.fulfillment.observedAt = next.updatedAt;
              assert.equal(latest.updatedAt, next.updatedAt);
            } else {
              assert.ok(Date.parse(latest.updatedAt) > Date.parse(next.updatedAt));
            }
            [items[0], items[1]] = [next, latest];
          }),
        `${page[2]} inverted ${key} order`,
        new RegExp(
          `${RegExp.escape(page[0])}\\.items\\[1\\] page order updatedAt desc, orderId asc`,
          "u",
        ),
      );
    });
  }
}

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

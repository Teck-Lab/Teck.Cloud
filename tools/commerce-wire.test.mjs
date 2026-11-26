import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import test from "node:test";
import { fileURLToPath } from "node:url";
import { parse } from "yaml";

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const contractRoot = path.join(repoRoot, "docs/contracts/commerce/v1");
const customer = parse(fs.readFileSync(path.join(contractRoot, "customer.openapi.yaml"), "utf8"));
const readme = fs.readFileSync(path.join(contractRoot, "README.md"), "utf8");
const listOrders = customer.paths["/v1/commerce/orders"].get;
const wire = customer["x-commerce-error-wire"];
const paging = listOrders["x-commerce-paging-example"];

const HTTP_STATUSES = [400, 401, 403, 404, 409, 422, 503];
const STANDARD_GRPC_CODES = new Map([
  ["INVALID_ARGUMENT", 3],
  ["UNAUTHENTICATED", 16],
  ["PERMISSION_DENIED", 7],
  ["NOT_FOUND", 5],
  ["ALREADY_EXISTS", 6],
  ["ABORTED", 10],
  ["FAILED_PRECONDITION", 9],
  ["UNAVAILABLE", 14],
]);
const FROZEN_WIRE = {
  invalid_request: { httpStatus: 400, grpcStatusCode: 3, grpcCode: "INVALID_ARGUMENT" },
  unauthenticated: { httpStatus: 401, grpcStatusCode: 16, grpcCode: "UNAUTHENTICATED" },
  forbidden: { httpStatus: 403, grpcStatusCode: 7, grpcCode: "PERMISSION_DENIED" },
  not_found: { httpStatus: 404, grpcStatusCode: 5, grpcCode: "NOT_FOUND" },
  idempotency_conflict: { httpStatus: 409, grpcStatusCode: 6, grpcCode: "ALREADY_EXISTS" },
  stale_revision: { httpStatus: 409, grpcStatusCode: 10, grpcCode: "ABORTED" },
  invalid_transition: { httpStatus: 409, grpcStatusCode: 9, grpcCode: "FAILED_PRECONDITION" },
  unsupported_option: { httpStatus: 422, grpcStatusCode: 9, grpcCode: "FAILED_PRECONDITION" },
  dependency_unavailable: { httpStatus: 503, grpcStatusCode: 14, grpcCode: "UNAVAILABLE" },
};

function failClosed() {
  return { httpStatus: wire.failClosed.httpStatus, code: wire.failClosed.code };
}

function dependencyOutcome({ operation, phase, grpcStatusCode, trailers }) {
  const confirmation = wire.licensingConfirmation;
  assert.ok(confirmation, "customer contract declares the licensing confirmation rule");
  if (confirmation.when !== "unsuccessful")
    throw new Error(`unexpected licensing confirmation when: ${confirmation.when}`);
  if (confirmation.action !== "fail-closed")
    throw new Error(`unexpected licensing confirmation action: ${confirmation.action}`);
  const licensingConfirmation =
    operation === confirmation.operation && phase === confirmation.phase && grpcStatusCode !== 0;
  if (licensingConfirmation) return failClosed();
  const licensing = wire.licensingNotFound;
  const licensingNotFound =
    operation === licensing.operation &&
    grpcStatusCode === licensing.grpcStatusCode &&
    trailers.length === wire.trailer.occurrences &&
    trailers[0] === "not_found";
  if (licensingNotFound) {
    if (licensing.otherwise !== "fail-closed")
      throw new Error(`unexpected licensing NOT_FOUND otherwise: ${licensing.otherwise}`);
    return phase === licensing.phase ? { action: licensing.action } : failClosed();
  }
  if (grpcStatusCode === 0) return trailers.length === 0 ? { ok: true } : failClosed();
  if (trailers.length !== wire.trailer.occurrences) return failClosed();
  const [value] = trailers;
  if (!new RegExp(wire.trailer.pattern, "u").test(value)) return failClosed();
  if (Buffer.from(value, "ascii").toString("ascii") !== value) return failClosed();
  const entry = wire.codes[value];
  if (!entry || entry.grpcStatusCode !== grpcStatusCode) return failClosed();
  return { httpStatus: entry.httpStatus, code: value };
}

function compareOrders(left, right) {
  return (
    Date.parse(right.updatedAt) - Date.parse(left.updatedAt) ||
    (left.orderId < right.orderId ? -1 : left.orderId > right.orderId ? 1 : 0)
  );
}

function applyRereads(page, rereads) {
  const replacements = new Map(rereads.map(({ orderId, snapshot }) => [orderId, snapshot]));
  return {
    items: page.items.map((order) => replacements.get(order.orderId) ?? order).sort(compareOrders),
    nextCursor: page.nextCursor,
  };
}

test("error wire map freezes every stable HTTP error to one gRPC pair", () => {
  assert.ok(wire, "customer contract declares x-commerce-error-wire");
  assert.deepEqual(wire.trailer, {
    key: "teck-commerce-error-code",
    encoding: "ascii",
    pattern: "^[a-z][a-z0-9_]*$",
    occurrences: 1,
    matching: "exact",
  });
  assert.deepEqual(wire.forwarding, { dependencyMessage: "never", detailSource: "gateway" });
  assert.equal(wire.failClosed.httpStatus, 503);
  assert.equal(wire.failClosed.code, "dependency_unavailable");
  assert.deepEqual(
    new Set(wire.failClosed.when),
    new Set(["malformed", "unknown_code", "missing_code", "status_code_mismatch"]),
  );
  assert.deepEqual(wire.codes, FROZEN_WIRE);

  const problem = customer.components.schemas.Problem;
  assert.deepEqual(
    new Set(problem.properties.code.enum),
    new Set(Object.keys(FROZEN_WIRE)),
    "every frozen code is a declared Problem code",
  );
  assert.deepEqual(
    new Set(problem.properties.status.enum),
    new Set(HTTP_STATUSES),
    "Problem statuses keep their existing HTTP set",
  );

  const pairs = new Set();
  for (const [code, entry] of Object.entries(wire.codes)) {
    assert.match(code, new RegExp(wire.trailer.pattern, "u"), `${code} is a lowercase ASCII code`);
    assert.equal(
      Buffer.from(code, "ascii").toString("ascii"),
      code,
      `${code} contains only ASCII characters`,
    );
    assert.ok(HTTP_STATUSES.includes(entry.httpStatus), `${code} uses an HTTP error status`);
    assert.equal(
      STANDARD_GRPC_CODES.get(entry.grpcCode),
      entry.grpcStatusCode,
      `${code} uses the canonical gRPC status/code pair`,
    );
    const pair = `${entry.httpStatus}:${entry.grpcCode}`;
    assert.equal(pairs.has(pair), false, `${code} does not duplicate another HTTP/gRPC pair`);
    pairs.add(pair);
  }
});

test("resolver accepts exactly the frozen pairs and fails closed otherwise", () => {
  for (const [code, entry] of Object.entries(FROZEN_WIRE))
    assert.deepEqual(
      dependencyOutcome({ grpcStatusCode: entry.grpcStatusCode, trailers: [code] }),
      { httpStatus: entry.httpStatus, code },
      `${code} resolves to its frozen HTTP error`,
    );

  assert.deepEqual(dependencyOutcome({ grpcStatusCode: 0, trailers: [] }), { ok: true });

  const rejected = [
    ["missing trailer", { grpcStatusCode: 3, trailers: [] }],
    ["duplicate trailer", { grpcStatusCode: 3, trailers: ["invalid_request", "invalid_request"] }],
    ["mismatched status", { grpcStatusCode: 3, trailers: ["forbidden"] }],
    ["mismatched 409 pair", { grpcStatusCode: 6, trailers: ["stale_revision"] }],
    ["trailing space", { grpcStatusCode: 3, trailers: ["invalid_request "] }],
    ["non-ASCII value", { grpcStatusCode: 3, trailers: ["invalid_request\u00e9"] }],
    ["uppercase value", { grpcStatusCode: 3, trailers: ["INVALID_REQUEST"] }],
    ["unknown code", { grpcStatusCode: 3, trailers: ["unknown_code"] }],
    ["code on success", { grpcStatusCode: 0, trailers: ["invalid_request"] }],
    ["unknown status", { grpcStatusCode: 2, trailers: ["invalid_request"] }],
  ];
  for (const [label, response] of rejected)
    assert.deepEqual(dependencyOutcome(response), failClosed(), `${label} fails closed as 503`);
});

test("Licensing NOT_FOUND keeps its single initial-phase reread exception", () => {
  const { operation, grpcStatusCode, grpcCode, action, phase, otherwise } = wire.licensingNotFound;
  assert.deepEqual(
    { operation, grpcStatusCode, grpcCode, action, phase, otherwise },
    {
      operation: "GetCurrentLicense",
      grpcStatusCode: 5,
      grpcCode: "NOT_FOUND",
      action: "reread-order-once",
      phase: "initial",
      otherwise: "fail-closed",
    },
  );
  assert.equal(
    STANDARD_GRPC_CODES.get(wire.licensingNotFound.grpcCode),
    wire.licensingNotFound.grpcStatusCode,
    "the special reread pair is canonical",
  );

  const licensingNotFound = (observedPhase) =>
    dependencyOutcome({
      operation,
      phase: observedPhase,
      grpcStatusCode,
      trailers: ["not_found"],
    });

  assert.deepEqual(licensingNotFound(phase), { action }, "the initial phase gets the reread");
  assert.deepEqual(licensingNotFound("initial"), { action }, "the initial phase is exact");
  for (const [label, observedPhase] of [
    ["absent", undefined],
    ["null", null],
    ["confirmation", "confirmation"],
    ["unknown", "reread"],
  ])
    assert.deepEqual(
      licensingNotFound(observedPhase),
      failClosed(),
      `a ${label} phase fails closed instead of the scoped 404`,
    );

  for (const ordinaryPhase of [undefined, "initial"])
    assert.deepEqual(
      dependencyOutcome({
        operation: "GetCustomerOrder",
        phase: ordinaryPhase,
        grpcStatusCode: 5,
        trailers: ["not_found"],
      }),
      { httpStatus: 404, code: "not_found" },
      "the ordinary scoped 404 still applies outside the licensing lookup",
    );

  for (const trailers of [[], ["forbidden"], ["not_found", "not_found"]])
    assert.deepEqual(
      dependencyOutcome({ operation, phase: "initial", grpcStatusCode, trailers }),
      failClosed(),
      "a malformed Licensing NOT_FOUND still fails closed",
    );
});

test("licensing confirmation fails closed ahead of generic mappings for every unsuccessful result", () => {
  const confirmation = wire.licensingConfirmation;
  assert.deepEqual(
    {
      operation: confirmation.operation,
      phase: confirmation.phase,
      when: confirmation.when,
      action: confirmation.action,
    },
    {
      operation: "GetCurrentLicense",
      phase: "confirmation",
      when: "unsuccessful",
      action: "fail-closed",
    },
  );
  assert.deepEqual(
    Object.keys(confirmation)
      .filter((key) => key !== "description")
      .sort(),
    ["action", "operation", "phase", "when"],
    "the confirmation rule declares exactly its matching fields",
  );

  const confirmationOutcome = (grpcStatusCode, trailers) =>
    dependencyOutcome({
      operation: confirmation.operation,
      phase: confirmation.phase,
      grpcStatusCode,
      trailers,
    });

  assert.deepEqual(
    confirmationOutcome(0, []),
    { ok: true },
    "a clean OK is not an error even though the caller still confirms the exact grant",
  );
  for (const [label, grpcStatusCode, trailers] of [
    ["canonical NOT_FOUND", 5, ["not_found"]],
    ["canonical PERMISSION_DENIED", 7, ["forbidden"]],
    ["canonical INVALID_ARGUMENT", 3, ["invalid_request"]],
    ["missing trailer", 7, []],
    ["duplicate trailer", 5, ["not_found", "not_found"]],
    ["unknown code", 7, ["unknown_code"]],
    ["mismatched pair", 5, ["forbidden"]],
    ["malformed success", 0, ["forbidden"]],
  ])
    assert.deepEqual(
      confirmationOutcome(grpcStatusCode, trailers),
      failClosed(),
      `${label} fails closed as 503 instead of taking a generic mapping`,
    );

  for (const [label, response, expected] of [
    [
      "an ordinary caller denial at confirmation",
      {
        operation: "GetCustomerOrder",
        phase: "confirmation",
        grpcStatusCode: 7,
        trailers: ["forbidden"],
      },
      { httpStatus: 403, code: "forbidden" },
    ],
    [
      "an Orders scoped 404 at confirmation",
      {
        operation: "GetCustomerOrder",
        phase: "confirmation",
        grpcStatusCode: 5,
        trailers: ["not_found"],
      },
      { httpStatus: 404, code: "not_found" },
    ],
    [
      "a canonical denial outside confirmation",
      {
        operation: confirmation.operation,
        phase: "initial",
        grpcStatusCode: 7,
        trailers: ["forbidden"],
      },
      { httpStatus: 403, code: "forbidden" },
    ],
    [
      "a canonical denial without a phase",
      { operation: confirmation.operation, grpcStatusCode: 7, trailers: ["forbidden"] },
      { httpStatus: 403, code: "forbidden" },
    ],
  ])
    assert.deepEqual(dependencyOutcome(response), expected, `${label} keeps its existing path`);

  assert.deepEqual(
    dependencyOutcome({
      operation: confirmation.operation,
      phase: "initial",
      grpcStatusCode: 5,
      trailers: ["not_found"],
    }),
    { action: wire.licensingNotFound.action },
    "the initial-only Licensing NOT_FOUND exception still applies outside confirmation",
  );
});

test("README error table matches the frozen wire map", () => {
  const errorsSection = readme.slice(
    readme.indexOf("## Errors"),
    readme.indexOf("## Compatibility and versioning"),
  );
  const rows = [
    ...errorsSection.matchAll(
      /^\|\s*(\d{3})\s*\|\s*`([a-z_]+)`\s*\|\s*(\d+)\s*\|\s*`([A-Z_]+)`\s*\|/gmu,
    ),
  ].map(([, httpStatus, code, grpcStatus, grpcCode]) => [
    code,
    {
      httpStatus: Number(httpStatus),
      grpcStatusCode: Number(grpcStatus),
      grpcCode,
    },
  ]);
  assert.equal(rows.length, Object.keys(FROZEN_WIRE).length, "README lists every frozen code once");
  assert.deepEqual(Object.fromEntries(rows), FROZEN_WIRE);
});

test("nonactive reread reorders the fixed page without refilling it", () => {
  const aId = "0f0e0d0c-0b0a-4999-8877-665544332211";
  const bId = "7d8e9fa0-b1c2-4d3e-8f40-516273849506";
  const cursor = "cursor_orders_page_2";
  const scope = {
    tenantId: "3f0f0f6e-6b1a-4d1e-9f4a-1c2b3d4e5f60",
    subjectId: "customer-subject-7f3a",
  };

  assert.deepEqual(paging.scope, scope);
  assert.deepEqual(paging.invariants, {
    membership: "unchanged",
    refill: "none",
    cursor: "original opaque value unchanged",
  });

  const originalItems = paging.originalPage.items;
  const originalA = originalItems.find(({ orderId }) => orderId === aId);
  const originalB = originalItems.find(({ orderId }) => orderId === bId);
  assert.deepEqual(
    originalItems.map(({ orderId }) => orderId),
    [aId, bId],
    "the original scoped page is A then B",
  );
  assert.ok(originalA, "the original page contains A");
  assert.ok(originalB, "the original page contains B");
  assert.equal(originalA.updatedAt, "2026-11-23T20:15:00Z");
  assert.equal(originalB.updatedAt, "2026-11-23T20:10:00Z");
  assert.equal(originalB.status, "active");
  assert.notEqual(originalB.license, null, "B is active on the original page");
  assert.equal(paging.originalPage.nextCursor, cursor);
  assert.deepEqual(
    originalItems,
    [...originalItems].sort(compareOrders),
    "the original page is already sorted by updatedAt desc, orderId asc",
  );

  assert.equal(paging.rereads.length, 1, "only one member is reread");
  const [reread] = paging.rereads;
  assert.equal(reread.orderId, bId);
  assert.deepEqual(reread.scope, scope, "the reread keeps the original scope");
  assert.equal(reread.snapshot.orderId, bId, "a reread replaces only its own order");
  assert.equal(reread.snapshot.status, "pendingReadiness");
  assert.equal(reread.snapshot.license, null, "the nonactive reread drops the license");
  assert.equal(reread.snapshot.updatedAt, "2026-11-23T20:16:00Z");
  assert.ok(
    Date.parse(reread.snapshot.updatedAt) > Date.parse(originalA.updatedAt),
    "the reread moves B ahead of A",
  );

  const emitted = paging.emittedPage;
  assert.deepEqual(applyRereads(paging.originalPage, paging.rereads), emitted);
  assert.deepEqual(
    emitted.items.map(({ orderId }) => orderId),
    [bId, aId],
    "the emitted page is B then A",
  );
  assert.equal(emitted.nextCursor, cursor, "the original opaque cursor is unchanged");
  assert.equal(emitted.nextCursor, paging.originalPage.nextCursor);
  assert.deepEqual(
    applyRereads({ ...paging.originalPage, nextCursor: null }, paging.rereads),
    {
      ...emitted,
      nextCursor: null,
    },
    "a terminal page stays terminal after reordering",
  );

  const originalIds = new Set(originalItems.map(({ orderId }) => orderId));
  const emittedIds = new Set(emitted.items.map(({ orderId }) => orderId));
  assert.equal(emitted.items.length, originalItems.length, "no order is added or removed");
  assert.deepEqual(emittedIds, originalIds, "page membership is unchanged");
  assert.equal(emittedIds.size, emitted.items.length, "no duplicate order is introduced");

  assert.deepEqual(
    emitted.items.find(({ orderId }) => orderId === aId),
    originalA,
    "the untouched order keeps its original projection",
  );
  assert.deepEqual(
    emitted.items.find(({ orderId }) => orderId === bId),
    reread.snapshot,
    "the reread order carries the replacement projection",
  );

  const tiedAt = "2026-11-23T20:16:00Z";
  const tied = [
    { orderId: bId, updatedAt: tiedAt },
    { orderId: aId, updatedAt: tiedAt },
  ].sort(compareOrders);
  assert.deepEqual(
    tied.map(({ orderId }) => orderId),
    [aId, bId],
    "equal updatedAt falls back to orderId ascending",
  );

  const nextCursor = customer.components.schemas.OrderPage.properties.nextCursor;
  assert.ok(
    nextCursor.oneOf.some(({ type }) => type === "null"),
    "nextCursor remains string or null",
  );
});

test("a still-active reread confirms once then fails the whole page closed without a partial result", () => {
  const scope = paging.scope;
  const [activeOrder] = paging.originalPage.items;
  const nonactiveOrder = paging.rereads[0].snapshot;
  assert.equal(activeOrder.status, "active", "the page starts from an active order");
  assert.equal(nonactiveOrder.status, "pendingReadiness", "a nonactive member needs no license");
  const originalGeneration = 41;
  const rereadGeneration = 42;
  const page = {
    items: [nonactiveOrder, { ...activeOrder, desiredRevision: originalGeneration }],
    nextCursor: paging.originalPage.nextCursor,
  };
  const rereadSnapshot = {
    ...activeOrder,
    revision: activeOrder.revision + 1,
    desiredRevision: rereadGeneration,
  };
  assert.equal(rereadSnapshot.status, "active", "the reread is still active");
  assert.notEqual(rereadSnapshot.desiredRevision, originalGeneration);

  function reader(confirmationReply) {
    const calls = [];
    const licensingReplies = [{ grpcStatusCode: 5, trailers: ["not_found"] }, confirmationReply];

    function licensingLookup({ operation, phase, orderId, desiredRevision }) {
      const reply = licensingReplies.shift();
      assert.ok(reply, "Licensing is called only for the contract's two phases");
      calls.push({
        operation,
        phase,
        tenantId: scope.tenantId,
        subjectId: scope.subjectId,
        orderId,
        desiredRevision,
      });
      return dependencyOutcome({ operation, phase, ...reply });
    }

    function rereadOrder(orderId) {
      calls.push({
        operation: "GetCustomerOrder",
        tenantId: scope.tenantId,
        subjectId: scope.subjectId,
        orderId,
      });
      return rereadSnapshot;
    }

    function unavailable(outcome) {
      return { httpStatus: outcome.httpStatus, code: outcome.code, page: null };
    }

    function verifyActiveOrder(order) {
      const initial = licensingLookup({
        operation: wire.licensingNotFound.operation,
        phase: "initial",
        orderId: order.orderId,
        desiredRevision: order.desiredRevision,
      });
      if (initial.ok === true) return { order };
      if (initial.action !== wire.licensingNotFound.action)
        return { unavailable: unavailable(initial) };
      const snapshot = rereadOrder(order.orderId);
      if (snapshot.status !== "active") return { order: snapshot };
      const confirmation = licensingLookup({
        operation: wire.licensingConfirmation.operation,
        phase: wire.licensingConfirmation.phase,
        orderId: snapshot.orderId,
        desiredRevision: snapshot.desiredRevision,
      });
      return confirmation.ok === true
        ? { order: snapshot }
        : { unavailable: unavailable(confirmation) };
    }

    function readPage() {
      const items = [];
      for (const order of page.items) {
        if (order.status !== "active") {
          items.push(order);
          continue;
        }
        const verified = verifyActiveOrder(order);
        if (verified.unavailable) return verified.unavailable;
        items.push(verified.order);
      }
      return { httpStatus: 200, items, nextCursor: page.nextCursor };
    }

    return { calls, readPage };
  }

  const expectedCalls = [
    {
      operation: "GetCurrentLicense",
      phase: "initial",
      tenantId: scope.tenantId,
      subjectId: scope.subjectId,
      orderId: activeOrder.orderId,
      desiredRevision: originalGeneration,
    },
    {
      operation: "GetCustomerOrder",
      tenantId: scope.tenantId,
      subjectId: scope.subjectId,
      orderId: activeOrder.orderId,
    },
    {
      operation: "GetCurrentLicense",
      phase: "confirmation",
      tenantId: scope.tenantId,
      subjectId: scope.subjectId,
      orderId: activeOrder.orderId,
      desiredRevision: rereadGeneration,
    },
  ];

  for (const [label, confirmationReply] of [
    ["a confirmation NOT_FOUND", { grpcStatusCode: 5, trailers: ["not_found"] }],
    [
      "a matching canonical PERMISSION_DENIED confirmation",
      { grpcStatusCode: 7, trailers: ["forbidden"] },
    ],
  ]) {
    const { calls, readPage } = reader(confirmationReply);
    const response = readPage();
    assert.deepEqual(
      response,
      { httpStatus: 503, code: "dependency_unavailable", page: null },
      `${label} fails the whole page closed`,
    );
    assert.equal(Object.hasOwn(response, "items"), false, `${label} returns no partial items`);
    assert.equal(
      Object.hasOwn(response, "nextCursor"),
      false,
      `${label} returns no cursor for an unconfirmed page`,
    );
    assert.deepEqual(
      calls,
      expectedCalls,
      `${label} keeps the initial lookup and exactly one original-scope reread`,
    );
    assert.equal(
      calls.filter(({ operation }) => operation === "GetCustomerOrder").length,
      1,
      `${label} performs no second reread`,
    );
    assert.equal(
      calls.filter(({ operation }) => operation === "GetCurrentLicense").length,
      2,
      `${label} performs no extra Licensing lookup`,
    );
  }
});

test("customer list keeps scoped not-found and whole-page unavailability responses", () => {
  assert.equal(listOrders.responses["404"].$ref, "#/components/responses/NotFound");
  assert.equal(listOrders.responses["503"].$ref, "#/components/responses/Unavailable");
});

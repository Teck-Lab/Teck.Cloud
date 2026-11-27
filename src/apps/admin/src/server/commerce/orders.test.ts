import { describe, expect, it } from "bun:test";
import {
  ACTOR_ID,
  callAt,
  EVIDENCE_REFERENCE,
  jsonResponse,
  makeAdminOrder,
  makeDeps,
  makeGateway,
  makeOrderPage,
  ORDER_ID,
  OTHER_TENANT_ID,
  PLACEMENT_ID,
  TENANT_ID,
} from "./fixtures.testing";
import { createAdminCommerce } from "./orders";
import type { AdminOrderView } from "./validate";

const headers = new Headers({ cookie: "teck-admin.session=secret" });
const FOREIGN_ORDER_ID = "9d8c7b6a-5f4e-4d3c-8b2a-190807060504";

describe("admin order reads", () => {
  it("reads fixed-size pages, passes the opaque cursor through encoding, then exhausts", async () => {
    const first = makeOrderPage([makeAdminOrder()], "opaque+/=cursor");
    const second = makeOrderPage([makeAdminOrder()], null);
    const { calls, fetch } = makeGateway([jsonResponse(200, first), jsonResponse(200, second)]);
    const commerce = createAdminCommerce(makeDeps(fetch));

    const pageOne = await commerce.listOrders(headers, {
      tenantId: TENANT_ID,
      cursor: null,
    });
    if (pageOne.kind !== "ok") throw new Error("expected first page");
    expect(pageOne.items).toHaveLength(1);
    expect(pageOne.nextCursor).toBe("opaque+/=cursor");
    expect(new URL(callAt(calls, 0).url).searchParams.get("limit")).toBe("25");
    expect(new URL(callAt(calls, 0).url).searchParams.get("cursor")).toBeNull();

    const pageTwo = await commerce.listOrders(headers, {
      tenantId: TENANT_ID,
      cursor: pageOne.nextCursor,
    });
    if (pageTwo.kind !== "ok") throw new Error("expected second page");
    expect(new URL(callAt(calls, 1).url).searchParams.get("limit")).toBe("25");
    expect(new URL(callAt(calls, 1).url).searchParams.get("cursor")).toBe("opaque+/=cursor");
    expect(pageTwo.nextCursor).toBeNull();
    expect(calls).toHaveLength(2);
  });

  it("starts a changed tenant at the first page without a stale cursor", async () => {
    const { calls, fetch } = makeGateway([
      jsonResponse(200, makeOrderPage([makeAdminOrder()], "tenant-a-cursor")),
      jsonResponse(200, makeOrderPage([makeAdminOrder()], null)),
    ]);
    const commerce = createAdminCommerce(makeDeps(fetch));

    const tenantA = await commerce.listOrders(headers, {
      tenantId: TENANT_ID,
      cursor: null,
    });
    if (tenantA.kind !== "ok") throw new Error("expected tenant A page");

    await commerce.listOrders(headers, {
      tenantId: OTHER_TENANT_ID,
      cursor: null,
    });

    const secondUrl = new URL(callAt(calls, 1).url);
    expect(secondUrl.pathname).toBe(`/v1/admin/commerce/tenants/${OTHER_TENANT_ID}/orders`);
    expect(secondUrl.searchParams.get("cursor")).toBeNull();
    expect(callAt(calls, 1).url).not.toContain("tenant-a-cursor");
  });

  it("does not fetch an invalid tenant or an oversized cursor", async () => {
    const { calls, fetch } = makeGateway([]);
    const commerce = createAdminCommerce(makeDeps(fetch));

    const badTenant = await commerce.listOrders(headers, {
      tenantId: "not-a-uuid",
      cursor: null,
    });
    const badCursor = await commerce.listOrders(headers, {
      tenantId: TENANT_ID,
      cursor: "x".repeat(513),
    });

    expect(badTenant.kind).toBe("invalidTenant");
    expect(badCursor.kind).toBe("cursorRejected");
    expect(calls).toHaveLength(0);
  });

  it("returns a first-page escape when the gateway rejects a cursor", async () => {
    const { calls, fetch } = makeGateway([
      jsonResponse(400, { status: 400, code: "invalid_request" }),
    ]);
    const commerce = createAdminCommerce(makeDeps(fetch));

    const result = await commerce.listOrders(headers, {
      tenantId: TENANT_ID,
      cursor: "expired-cursor",
    });

    expect(result.kind).toBe("cursorRejected");
    if (result.kind !== "cursorRejected") throw new Error("expected rejection");
    expect(result.firstPageHref).toBe(`/orders?tenantId=${TENANT_ID}`);
    expect(calls).toHaveLength(1);
  });

  it("returns an empty first page without inventing rows", async () => {
    const { fetch } = makeGateway([jsonResponse(200, makeOrderPage([], null))]);
    const commerce = createAdminCommerce(makeDeps(fetch));

    const result = await commerce.listOrders(headers, {
      tenantId: TENANT_ID,
      cursor: null,
    });

    expect(result.kind).toBe("ok");
    if (result.kind !== "ok") throw new Error("expected empty page");
    expect(result.items).toEqual([]);
    expect(result.nextCursor).toBeNull();
  });

  it("does not fetch a detail request for non-UUID route values", async () => {
    const { calls, fetch } = makeGateway([]);
    const commerce = createAdminCommerce(makeDeps(fetch));

    const result = await commerce.getOrder(headers, {
      tenantId: TENANT_ID,
      orderId: "../../etc/passwd",
    });

    expect(result.kind).toBe("invalid");
    expect(calls).toHaveLength(0);
  });

  it("rejects a successful detail body that names a different order", async () => {
    const foreign = makeAdminOrder({ orderId: FOREIGN_ORDER_ID });
    const { calls, fetch } = makeGateway([jsonResponse(200, foreign)]);
    const commerce = createAdminCommerce(makeDeps(fetch));

    const result = await commerce.getOrder(headers, {
      tenantId: TENANT_ID,
      orderId: ORDER_ID,
    });

    expect(result.kind).toBe("error");
    expect(JSON.stringify(result)).not.toContain(FOREIGN_ORDER_ID);
    expect(calls).toHaveLength(1);
  });

  it("projects only known order fields and known nested fields", async () => {
    const base = makeAdminOrder();
    const leaky: Record<string, unknown> = {
      ...base,
      license: { document: "foreign-license-document", keyId: "K1", revision: 1 },
      provider: "provider-reference",
      customerEmail: "customer@example.test",
      payment: { ...base.payment, cardLast4: "card-last4-secret" },
      approval: { ...base.approval, staffEmail: "staff@example.test" },
      fulfillment: { ...base.fulfillment, providerTrace: "trace-reference" },
      audit: [
        {
          orderId: ORDER_ID,
          actorId: ACTOR_ID,
          action: "approve",
          reason: "Verified purchase order.",
          occurredAt: "2026-11-23T20:05:00Z",
          privateNote: "private-audit-note",
        },
      ],
    };
    const { fetch } = makeGateway([
      jsonResponse(200, leaky),
      jsonResponse(200, { items: [leaky], nextCursor: null, internalTrace: "page-trace" }),
    ]);
    const commerce = createAdminCommerce(makeDeps(fetch));

    const result = await commerce.getOrder(headers, {
      tenantId: TENANT_ID,
      orderId: ORDER_ID,
    });

    expect(result.kind).toBe("ok");
    if (result.kind !== "ok") throw new Error("expected detail");
    expect(result.order.orderId).toBe(ORDER_ID);
    expect(result.order.audit).toHaveLength(1);
    expect(result.order.audit[0]?.action).toBe("approve");

    const page = await commerce.listOrders(headers, {
      tenantId: TENANT_ID,
      cursor: null,
    });
    expect(page.kind).toBe("ok");

    const serialized = JSON.stringify([result, page]);
    for (const secret of [
      "foreign-license-document",
      "provider-reference",
      "customer@example.test",
      "card-last4-secret",
      "staff@example.test",
      "trace-reference",
      "private-audit-note",
      "page-trace",
    ]) {
      expect(serialized).not.toContain(secret);
    }
  });

  it("keeps an unknown additive order status for neutral display", async () => {
    const additive: AdminOrderView = { ...makeAdminOrder(), status: "pendingReview" };
    const { fetch } = makeGateway([
      jsonResponse(200, additive),
      jsonResponse(200, { items: [additive], nextCursor: null }),
    ]);
    const commerce = createAdminCommerce(makeDeps(fetch));

    const detail = await commerce.getOrder(headers, {
      tenantId: TENANT_ID,
      orderId: ORDER_ID,
    });
    expect(detail.kind).toBe("ok");
    if (detail.kind !== "ok") throw new Error("expected detail");
    expect(detail.order.status).toBe("pendingReview");

    const page = await commerce.listOrders(headers, {
      tenantId: TENANT_ID,
      cursor: null,
    });
    expect(page.kind).toBe("ok");
    if (page.kind !== "ok") throw new Error("expected page");
    expect(page.items).toHaveLength(1);
    expect(JSON.stringify(page)).toContain('"status":"pendingReview"');
  });
});

describe("admin order mutations", () => {
  it("records a decision with a trimmed reason and expected revision", async () => {
    const updated = makeAdminOrder({ revision: 5 });
    const { calls, fetch } = makeGateway([jsonResponse(200, updated)]);
    const commerce = createAdminCommerce(makeDeps(fetch));

    const result = await commerce.decideOrder(headers, {
      tenantId: TENANT_ID,
      orderId: ORDER_ID,
      decision: "approve",
      reason: "  Verified purchase order.  ",
      expectedRevision: 4,
    });

    expect(result.kind).toBe("updated");
    expect(calls).toHaveLength(1);
    const call = calls[0];
    expect(call?.init.method).toBe("POST");
    expect(call?.url).toBe(
      `https://gateway.example/v1/admin/commerce/tenants/${TENANT_ID}/orders/${ORDER_ID}/decision`,
    );
    expect(call?.init.body).toBe(
      JSON.stringify({
        decision: "approve",
        reason: "Verified purchase order.",
        expectedRevision: 4,
      }),
    );
    expect(new Headers(call?.init.headers).get("content-type")).toBe("application/json");
  });

  it("reads the current order once after a stale revision and never resubmits", async () => {
    const current = makeAdminOrder({ revision: 6, status: "pendingReadiness" });
    const { calls, fetch } = makeGateway([
      jsonResponse(409, { status: 409, code: "stale_revision" }),
      jsonResponse(200, current),
    ]);
    const commerce = createAdminCommerce(makeDeps(fetch));

    const result = await commerce.decideOrder(headers, {
      tenantId: TENANT_ID,
      orderId: ORDER_ID,
      decision: "reject",
      reason: "Purchase order mismatch.",
      expectedRevision: 4,
    });

    expect(result.kind).toBe("conflict");
    if (result.kind !== "conflict") throw new Error("expected conflict");
    expect(result.code).toBe("stale_revision");
    expect(result.order?.revision).toBe(6);
    expect(result.order?.approval.status).toBe("approved");
    expect(calls.map((call) => call.init.method)).toEqual(["POST", "GET"]);
    expect(calls).toHaveLength(2);
    expect(calls[1]?.url).toBe(
      `https://gateway.example/v1/admin/commerce/tenants/${TENANT_ID}/orders/${ORDER_ID}`,
    );
    expect(calls.filter((call) => call.init.method === "POST")).toHaveLength(1);
  });

  it("treats a final-state conflict as a conflict and still reads current state once", async () => {
    const current = makeAdminOrder({ status: "rejected", revision: 5 });
    const { calls, fetch } = makeGateway([
      jsonResponse(409, { status: 409, code: "invalid_transition" }),
      jsonResponse(200, current),
    ]);
    const commerce = createAdminCommerce(makeDeps(fetch));

    const result = await commerce.decideOrder(headers, {
      tenantId: TENANT_ID,
      orderId: ORDER_ID,
      decision: "approve",
      reason: "Second attempt.",
      expectedRevision: 4,
    });

    expect(result.kind).toBe("conflict");
    expect(calls.map((call) => call.init.method)).toEqual(["POST", "GET"]);
  });

  it("does not refetch or retry on a transient failure", async () => {
    const { calls, fetch } = makeGateway([
      jsonResponse(503, { status: 503, code: "dependency_unavailable" }),
    ]);
    const commerce = createAdminCommerce(makeDeps(fetch));

    const result = await commerce.decideOrder(headers, {
      tenantId: TENANT_ID,
      orderId: ORDER_ID,
      decision: "approve",
      reason: "Verified.",
      expectedRevision: 4,
    });

    expect(result.kind).toBe("unavailable");
    expect(calls).toHaveLength(1);
  });

  it("does not send a reasonless decision or force an invalid transition", async () => {
    const { calls, fetch } = makeGateway([
      jsonResponse(409, { status: 409, code: "something_else" }),
    ]);
    const commerce = createAdminCommerce(makeDeps(fetch));

    const reasonless = await commerce.decideOrder(headers, {
      tenantId: TENANT_ID,
      orderId: ORDER_ID,
      decision: "approve",
      reason: "   ",
      expectedRevision: 4,
    });
    expect(reasonless.kind).toBe("invalid");
    expect(calls).toHaveLength(0);

    const unknownConflict = await commerce.decideOrder(headers, {
      tenantId: TENANT_ID,
      orderId: ORDER_ID,
      decision: "approve",
      reason: "Verified.",
      expectedRevision: 4,
    });
    expect(unknownConflict.kind).toBe("error");
    expect(calls).toHaveLength(1);
  });

  it("maps mutation denial, expiry and missing orders without retrying", async () => {
    const { calls, fetch } = makeGateway([
      jsonResponse(403, { status: 403, code: "forbidden" }),
      jsonResponse(401, { status: 401, code: "unauthenticated" }),
      jsonResponse(404, { status: 404, code: "not_found" }),
    ]);
    const commerce = createAdminCommerce(makeDeps(fetch));

    const forbidden = await commerce.decideOrder(headers, {
      tenantId: TENANT_ID,
      orderId: ORDER_ID,
      decision: "approve",
      reason: "Verified.",
      expectedRevision: 4,
    });
    const expired = await commerce.decideOrder(headers, {
      tenantId: TENANT_ID,
      orderId: ORDER_ID,
      decision: "approve",
      reason: "Verified.",
      expectedRevision: 4,
    });
    const missing = await commerce.decideOrder(headers, {
      tenantId: TENANT_ID,
      orderId: ORDER_ID,
      decision: "approve",
      reason: "Verified.",
      expectedRevision: 4,
    });

    expect(forbidden.kind).toBe("forbidden");
    expect(expired.kind).toBe("unauthorized");
    expect(missing.kind).toBe("notFound");
    expect(calls.map((call) => call.init.method)).toEqual(["POST", "POST", "POST"]);
  });

  it("does not accept a mutation response that names a different order", async () => {
    const foreign = makeAdminOrder({ orderId: FOREIGN_ORDER_ID, revision: 5 });
    const { calls, fetch } = makeGateway([jsonResponse(200, foreign)]);
    const commerce = createAdminCommerce(makeDeps(fetch));

    const result = await commerce.decideOrder(headers, {
      tenantId: TENANT_ID,
      orderId: ORDER_ID,
      decision: "approve",
      reason: "Verified.",
      expectedRevision: 4,
    });

    expect(result.kind).toBe("error");
    expect(JSON.stringify(result)).not.toContain(FOREIGN_ORDER_ID);
    expect(calls.map((call) => call.init.method)).toEqual(["POST"]);
  });

  it("never surfaces a foreign current order after a conflict", async () => {
    const foreign = makeAdminOrder({ orderId: FOREIGN_ORDER_ID, revision: 6 });
    const { calls, fetch } = makeGateway([
      jsonResponse(409, { status: 409, code: "stale_revision" }),
      jsonResponse(200, foreign),
    ]);
    const commerce = createAdminCommerce(makeDeps(fetch));

    const result = await commerce.decideOrder(headers, {
      tenantId: TENANT_ID,
      orderId: ORDER_ID,
      decision: "reject",
      reason: "Purchase order mismatch.",
      expectedRevision: 4,
    });

    expect(result.kind).toBe("conflict");
    if (result.kind !== "conflict") throw new Error("expected conflict");
    expect(result.order).toBeNull();
    expect(JSON.stringify(result)).not.toContain(FOREIGN_ORDER_ID);
    expect(calls.map((call) => call.init.method)).toEqual(["POST", "GET"]);
  });

  it("keeps an additive status from a mutation response without inventing actions", async () => {
    const additive: AdminOrderView = {
      ...makeAdminOrder({ revision: 5 }),
      status: "pendingReview",
    };
    const { fetch } = makeGateway([jsonResponse(200, additive)]);
    const commerce = createAdminCommerce(makeDeps(fetch));

    const result = await commerce.decideOrder(headers, {
      tenantId: TENANT_ID,
      orderId: ORDER_ID,
      decision: "approve",
      reason: "Verified.",
      expectedRevision: 4,
    });

    expect(result.kind).toBe("updated");
    if (result.kind !== "updated") throw new Error("expected update");
    expect(JSON.stringify(result)).toContain('"status":"pendingReview"');
  });

  it("records UUID placement evidence without any force-ready flag", async () => {
    const updated = makeAdminOrder({ revision: 5 });
    const { calls, fetch } = makeGateway([jsonResponse(200, updated)]);
    const commerce = createAdminCommerce(makeDeps(fetch));

    const result = await commerce.recordPlacementEvidence(headers, {
      tenantId: TENANT_ID,
      orderId: ORDER_ID,
      placementId: PLACEMENT_ID,
      evidenceReference: `  ${EVIDENCE_REFERENCE}  `,
      expectedRevision: 4,
    });

    expect(result.kind).toBe("updated");
    const call = calls[0];
    expect(call?.url).toBe(
      `https://gateway.example/v1/admin/commerce/tenants/${TENANT_ID}/orders/${ORDER_ID}/placement-evidence`,
    );
    expect(call?.init.body).toBe(
      JSON.stringify({
        placementId: PLACEMENT_ID,
        evidenceReference: EVIDENCE_REFERENCE,
        expectedRevision: 4,
      }),
    );
    expect(String(call?.init.body)).not.toContain("ready");
    expect(String(call?.init.body)).not.toContain("force");
  });

  it("rejects malformed placement evidence before any request", async () => {
    const { calls, fetch } = makeGateway([]);
    const commerce = createAdminCommerce(makeDeps(fetch));

    const result = await commerce.recordPlacementEvidence(headers, {
      tenantId: TENANT_ID,
      orderId: ORDER_ID,
      placementId: "not-a-uuid",
      evidenceReference: "evidence://ops/example",
      expectedRevision: 4,
    });

    expect(result.kind).toBe("invalid");
    expect(calls).toHaveLength(0);
  });

  it("reads the current order once after an evidence conflict", async () => {
    const current = makeAdminOrder({ revision: 8 });
    const { calls, fetch } = makeGateway([
      jsonResponse(409, { status: 409, code: "stale_revision" }),
      jsonResponse(200, current),
    ]);
    const commerce = createAdminCommerce(makeDeps(fetch));

    const result = await commerce.recordPlacementEvidence(headers, {
      tenantId: TENANT_ID,
      orderId: ORDER_ID,
      placementId: PLACEMENT_ID,
      evidenceReference: EVIDENCE_REFERENCE,
      expectedRevision: 4,
    });

    expect(result.kind).toBe("conflict");
    expect(calls.map((call) => call.init.method)).toEqual(["POST", "GET"]);
  });
});

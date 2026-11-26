import type { AdminOrder, AdminOrderPage, CommerceDeps, FetchLike } from "./types";

export const TENANT_ID = "3f0f0f6e-6b1a-4d1e-9f4a-1c2b3d4e5f60";
export const OTHER_TENANT_ID = "8a7b6c5d-4e3f-4210-9a8b-7c6d5e4f3021";
export const ORDER_ID = "2b3c4d5e-6f70-4a1b-9c2d-3e4f50617283";
export const REJECTED_ORDER_ID = "7c8d9e0f-a1b2-4c3d-8e4f-506172839405";
export const SUBSCRIPTION_ID = "4c5d6e7f-8091-4b2c-8d3e-4f5061728394";
export const ACTOR_ID = "f0e1d2c3-b4a5-4968-8778-99aabbccddee";
export const PLACEMENT_ID = "5d6e7f80-91a2-4c3d-9e4f-506172839405";
export const EVIDENCE_REFERENCE = "evidence://ops/2026-11-23/dedicated-primary-check";

const baseOrder: AdminOrder = {
  orderId: ORDER_ID,
  subscriptionId: SUBSCRIPTION_ID,
  tier: "dedicated",
  addOnCodes: ["replicaReadAccess"],
  total: { amountMinor: 3500, currency: "USD" },
  status: "pendingReadiness",
  revision: 4,
  createdAt: "2026-11-23T19:05:00Z",
  updatedAt: "2026-11-23T20:10:00Z",
  payment: { status: "paid", paidThrough: "2026-12-23T20:00:00Z" },
  approval: {
    status: "approved",
    actorId: ACTOR_ID,
    reason: "Verified purchase order and tenant intent.",
    occurredAt: "2026-11-23T20:05:00Z",
  },
  fulfillment: {
    status: "verifying",
    placementId: PLACEMENT_ID,
    evidenceReference: EVIDENCE_REFERENCE,
    observedAt: null,
    failureCode: null,
  },
  audit: [
    {
      orderId: ORDER_ID,
      actorId: ACTOR_ID,
      action: "approve",
      reason: "Verified purchase order and tenant intent.",
      occurredAt: "2026-11-23T20:05:00Z",
    },
    {
      orderId: ORDER_ID,
      actorId: ACTOR_ID,
      action: "recordPlacementEvidence",
      reason: EVIDENCE_REFERENCE,
      occurredAt: "2026-11-23T20:10:00Z",
    },
  ],
};

export function makeAdminOrder(overrides: Partial<AdminOrder> = {}): AdminOrder {
  return { ...baseOrder, ...overrides };
}

export function makePendingApprovalOrder(): AdminOrder {
  return makeAdminOrder({
    status: "pendingApproval",
    revision: 2,
    updatedAt: "2026-11-23T19:30:00Z",
    approval: {
      status: "pending",
      actorId: null,
      reason: null,
      occurredAt: null,
    },
    fulfillment: {
      status: "awaitingEvidence",
      placementId: null,
      evidenceReference: null,
      observedAt: null,
      failureCode: null,
    },
    audit: [],
  });
}

export function makeRejectedOrder(): AdminOrder {
  return makeAdminOrder({
    orderId: REJECTED_ORDER_ID,
    status: "rejected",
    revision: 3,
    tier: "shared",
    addOnCodes: [],
    total: { amountMinor: 1000, currency: "USD" },
    approval: {
      status: "rejected",
      actorId: ACTOR_ID,
      reason: "Purchase order did not match the requesting organization.",
      occurredAt: "2026-11-23T20:25:00Z",
    },
    fulfillment: {
      status: "awaitingEvidence",
      placementId: null,
      evidenceReference: null,
      observedAt: null,
      failureCode: null,
    },
    audit: [
      {
        orderId: REJECTED_ORDER_ID,
        actorId: ACTOR_ID,
        action: "reject",
        reason: "Purchase order did not match the requesting organization.",
        occurredAt: "2026-11-23T20:25:00Z",
      },
    ],
  });
}

export function makeOrderPage(
  items: AdminOrder[],
  nextCursor: string | null = null,
): AdminOrderPage {
  return { items, nextCursor };
}

export type GatewayCall = {
  url: string;
  init: RequestInit;
};

type QueuedResponse = Response | (() => Response);

export function makeGateway(responses: QueuedResponse[]): {
  calls: GatewayCall[];
  fetch: FetchLike;
} {
  const calls: GatewayCall[] = [];
  const fetch: FetchLike = async (url, init) => {
    calls.push({ url, init });
    const next = responses.shift();
    if (next === undefined) throw new Error("Unexpected gateway request");
    return typeof next === "function" ? next() : next;
  };
  return { calls, fetch };
}

export function makeDeps(fetch: FetchLike, overrides: Partial<CommerceDeps> = {}): CommerceDeps {
  return {
    gatewayUrl: "https://gateway.example/",
    fetch,
    getAccessToken: async () => "fixture-token",
    getScope: async () => "fixture-scope",
    ...overrides,
  };
}

export function jsonResponse(status: number, body: unknown): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "content-type": "application/json" },
  });
}

export function requestHeader(call: GatewayCall, name: string): string | null {
  return new Headers(call.init.headers).get(name);
}

export function callAt(calls: GatewayCall[], index: number): GatewayCall {
  const call = calls[index];
  if (call === undefined) throw new Error(`Missing gateway call ${index}`);
  return call;
}

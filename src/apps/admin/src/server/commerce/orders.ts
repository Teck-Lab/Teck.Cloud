import {
  ADMIN_ORDER_DECISION_PATH,
  ADMIN_ORDER_PATH,
  ADMIN_ORDERS_PATH,
  ADMIN_PLACEMENT_EVIDENCE_PATH,
  type CommerceDeps,
  type DecisionInput,
  MAX_EVIDENCE_REFERENCE_LENGTH,
  MAX_REASON_LENGTH,
  MAX_REVISION,
  MESSAGES,
  type MutationResult,
  ORDERS_PAGE_LIMIT,
  type OrdersLoadResult,
  type PlacementEvidenceInput,
  UUID_PATTERN,
} from "./types";
import {
  type AdminOrderView,
  isCursor,
  isUuid,
  problemCode,
  readAdminOrderPageView,
  readAdminOrderView,
} from "./validate";

type Identity =
  | { kind: "ok"; token: string; scope: string }
  | { kind: "unauthorized" }
  | { kind: "unavailable" };

type GatewayResponse =
  | {
      kind: "response";
      status: number;
      code: string | null;
      body: unknown;
    }
  | { kind: "unavailable" };

export type OrderDetailResult =
  | { kind: "ok"; tenantId: string; orderId: string; order: AdminOrderView }
  | { kind: "invalid"; message: string }
  | { kind: "unauthorized" }
  | { kind: "forbidden" }
  | { kind: "notFound" }
  | { kind: "unavailable" }
  | { kind: "error"; message: string };

export type AdminCommerce = {
  listOrders(
    headers: Headers,
    input: { tenantId: string; cursor: string | null },
  ): Promise<OrdersLoadResult>;
  getOrder(
    headers: Headers,
    input: { tenantId: string; orderId: string },
  ): Promise<OrderDetailResult>;
  decideOrder(
    headers: Headers,
    input: { tenantId: string; orderId: string } & DecisionInput,
  ): Promise<MutationResult>;
  recordPlacementEvidence(
    headers: Headers,
    input: { tenantId: string; orderId: string } & PlacementEvidenceInput,
  ): Promise<MutationResult>;
};

async function resolveIdentity(headers: Headers, deps: CommerceDeps): Promise<Identity> {
  try {
    const [token, scope] = await Promise.all([
      deps.getAccessToken(headers),
      deps.getScope(headers),
    ]);
    if (!token || !scope) return { kind: "unauthorized" };
    return { kind: "ok", token, scope };
  } catch {
    return { kind: "unavailable" };
  }
}

function gatewayUrl(deps: CommerceDeps, path: string): string | null {
  const configured = deps.gatewayUrl.trim();
  if (configured.length === 0) return null;
  try {
    const base = configured.endsWith("/") ? configured : `${configured}/`;
    return new URL(path.replace(/^\//u, ""), base).toString();
  } catch {
    return null;
  }
}

async function readJson(response: Response): Promise<unknown> {
  const text = await response.text();
  if (text.length === 0) return null;
  try {
    return JSON.parse(text);
  } catch {
    return null;
  }
}

async function request(
  deps: CommerceDeps,
  input: {
    bearer: string;
    method: "GET" | "POST";
    path: string;
    body?: unknown;
  },
): Promise<GatewayResponse> {
  const url = gatewayUrl(deps, input.path);
  if (url === null) return { kind: "unavailable" };

  const headers: Record<string, string> = {
    accept: "application/json",
    authorization: `Bearer ${input.bearer}`,
  };
  if (input.body !== undefined) headers["content-type"] = "application/json";

  try {
    const response = await deps.fetch(url, {
      method: input.method,
      headers,
      body: input.body === undefined ? undefined : JSON.stringify(input.body),
      cache: "no-store",
      redirect: "error",
      credentials: "omit",
    });
    const body = await readJson(response);
    return { kind: "response", status: response.status, code: problemCode(body), body };
  } catch {
    return { kind: "unavailable" };
  }
}

function orderPath(tenantId: string, orderId: string): string {
  return ADMIN_ORDER_PATH.replace("{tenantId}", encodeURIComponent(tenantId)).replace(
    "{orderId}",
    encodeURIComponent(orderId),
  );
}

function decisionPath(tenantId: string, orderId: string): string {
  return ADMIN_ORDER_DECISION_PATH.replace("{tenantId}", encodeURIComponent(tenantId)).replace(
    "{orderId}",
    encodeURIComponent(orderId),
  );
}

function evidencePath(tenantId: string, orderId: string): string {
  return ADMIN_PLACEMENT_EVIDENCE_PATH.replace("{tenantId}", encodeURIComponent(tenantId)).replace(
    "{orderId}",
    encodeURIComponent(orderId),
  );
}

function ordersPath(tenantId: string, cursor: string | null): string {
  const search = new URLSearchParams({ limit: String(ORDERS_PAGE_LIMIT) });
  if (cursor !== null) search.set("cursor", cursor);
  return `${ADMIN_ORDERS_PATH.replace(
    "{tenantId}",
    encodeURIComponent(tenantId),
  )}?${search.toString()}`;
}

function mapAuthFailure(identity: Identity): { kind: "unauthorized" } | { kind: "unavailable" } {
  return identity.kind === "unavailable" ? { kind: "unavailable" } : { kind: "unauthorized" };
}

type OrderResponse =
  | { kind: "ok"; order: AdminOrderView }
  | { kind: "notFound" }
  | { kind: "forbidden" }
  | { kind: "unauthorized" }
  | { kind: "unavailable" }
  | { kind: "error" };

/**
 * Reads one order only when the response names the exact order that was
 * requested. A shape-valid body for another order must never reach a detail
 * view, a mutation result or the current-state reread after a conflict.
 */
function readOrderResponse(response: GatewayResponse, expectedOrderId: string): OrderResponse {
  if (response.kind === "unavailable") return { kind: "unavailable" };
  if (response.status === 200) {
    const order = readAdminOrderView(response.body);
    if (order === null || order.orderId !== expectedOrderId) return { kind: "error" };
    return { kind: "ok", order };
  }
  if (response.status === 401) return { kind: "unauthorized" };
  if (response.status === 403) return { kind: "forbidden" };
  if (response.status === 404) return { kind: "notFound" };
  if (response.status === 503) return { kind: "unavailable" };
  return { kind: "error" };
}

async function refetchAfterConflict(
  deps: CommerceDeps,
  identity: { token: string },
  tenantId: string,
  orderId: string,
): Promise<AdminOrderView | null> {
  const response = await request(deps, {
    bearer: identity.token,
    method: "GET",
    path: orderPath(tenantId, orderId),
  });
  const result = readOrderResponse(response, orderId);
  return result.kind === "ok" ? result.order : null;
}

function validateDecision(input: DecisionInput): string | null {
  if (input.decision !== "approve" && input.decision !== "reject") {
    return "Choose approve or reject.";
  }
  const reason = input.reason.trim();
  if (reason.length === 0) return "A reason is required.";
  if (reason.length > MAX_REASON_LENGTH) {
    return `The reason must be at most ${MAX_REASON_LENGTH} characters.`;
  }
  if (
    !Number.isInteger(input.expectedRevision) ||
    input.expectedRevision < 1 ||
    input.expectedRevision > MAX_REVISION
  ) {
    return "The order revision is not valid.";
  }
  return null;
}

function validateEvidence(input: PlacementEvidenceInput): string | null {
  if (!UUID_PATTERN.test(input.placementId)) {
    return "Enter the placement UUID.";
  }
  const reference = input.evidenceReference.trim();
  if (reference.length === 0) return "A manual evidence reference is required.";
  if (reference.length > MAX_EVIDENCE_REFERENCE_LENGTH) {
    return `The evidence reference must be at most ${MAX_EVIDENCE_REFERENCE_LENGTH} characters.`;
  }
  if (
    !Number.isInteger(input.expectedRevision) ||
    input.expectedRevision < 1 ||
    input.expectedRevision > MAX_REVISION
  ) {
    return "The order revision is not valid.";
  }
  return null;
}

async function mutate(
  deps: CommerceDeps,
  headers: Headers,
  input: {
    tenantId: string;
    orderId: string;
    path: string;
    body: unknown;
    invalidMessage: string | null;
  },
): Promise<MutationResult> {
  if (!isUuid(input.tenantId) || !isUuid(input.orderId)) {
    return { kind: "invalid", message: input.invalidMessage ?? "The order link is not valid." };
  }
  if (input.invalidMessage !== null) {
    return { kind: "invalid", message: input.invalidMessage };
  }

  const identity = await resolveIdentity(headers, deps);
  if (identity.kind !== "ok") return mapAuthFailure(identity);

  const response = await request(deps, {
    bearer: identity.token,
    method: "POST",
    path: input.path,
    body: input.body,
  });
  if (response.kind === "unavailable") return { kind: "unavailable" };
  if (response.status === 200) {
    const order = readAdminOrderView(response.body);
    if (order === null || order.orderId !== input.orderId) {
      return { kind: "error", message: MESSAGES.genericError };
    }
    return { kind: "updated", order };
  }
  if (response.status === 401) return { kind: "unauthorized" };
  if (response.status === 403) return { kind: "forbidden" };
  if (response.status === 404) return { kind: "notFound" };
  if (response.status === 409) {
    const code = response.code;
    if (code !== "stale_revision" && code !== "invalid_transition") {
      return { kind: "error", message: MESSAGES.genericError };
    }
    // A conflict is resolved by reading the authoritative current order once.
    // The mutation is never replayed and no state is forced client-side.
    const current = await refetchAfterConflict(deps, identity, input.tenantId, input.orderId);
    return {
      kind: "conflict",
      code,
      order: current,
    };
  }
  if (response.status === 503) return { kind: "unavailable" };
  return { kind: "error", message: MESSAGES.genericError };
}

export function createAdminCommerce(deps: CommerceDeps): AdminCommerce {
  return {
    async listOrders(headers, input) {
      if (!isUuid(input.tenantId)) {
        return { kind: "invalidTenant", message: MESSAGES.invalidTenant };
      }
      if (input.cursor !== null && !isCursor(input.cursor)) {
        return {
          kind: "cursorRejected",
          message: MESSAGES.rejectedCursor,
          firstPageHref: `/orders?tenantId=${encodeURIComponent(input.tenantId)}`,
        };
      }

      const identity = await resolveIdentity(headers, deps);
      if (identity.kind !== "ok") return mapAuthFailure(identity);

      const response = await request(deps, {
        bearer: identity.token,
        method: "GET",
        path: ordersPath(input.tenantId, input.cursor),
      });
      if (response.kind === "unavailable") return { kind: "unavailable" };
      if (response.status === 200) {
        const page = readAdminOrderPageView(response.body);
        if (page === null) {
          return { kind: "error", message: MESSAGES.genericError };
        }
        return {
          kind: "ok",
          tenantId: input.tenantId,
          cursor: input.cursor,
          items: page.items,
          nextCursor: page.nextCursor,
          scope: identity.scope,
          baseHref: `/orders?tenantId=${encodeURIComponent(input.tenantId)}`,
        };
      }
      if (response.status === 400) {
        return input.cursor === null
          ? { kind: "error", message: MESSAGES.genericError }
          : {
              kind: "cursorRejected",
              message: MESSAGES.rejectedCursor,
              firstPageHref: `/orders?tenantId=${encodeURIComponent(input.tenantId)}`,
            };
      }
      if (response.status === 401) return { kind: "unauthorized" };
      if (response.status === 403) return { kind: "forbidden" };
      if (response.status === 404) {
        return { kind: "notFound", message: MESSAGES.tenantNotFound };
      }
      if (response.status === 503) return { kind: "unavailable" };
      return { kind: "error", message: MESSAGES.genericError };
    },

    async getOrder(headers, input) {
      if (!isUuid(input.tenantId) || !isUuid(input.orderId)) {
        return { kind: "invalid", message: MESSAGES.invalidOrder };
      }

      const identity = await resolveIdentity(headers, deps);
      if (identity.kind !== "ok") return mapAuthFailure(identity);

      const response = await request(deps, {
        bearer: identity.token,
        method: "GET",
        path: orderPath(input.tenantId, input.orderId),
      });
      const result = readOrderResponse(response, input.orderId);
      if (result.kind === "ok") {
        return {
          kind: "ok",
          tenantId: input.tenantId,
          orderId: input.orderId,
          order: result.order,
        };
      }
      if (result.kind === "error") return { kind: "error", message: MESSAGES.genericError };
      return result;
    },

    async decideOrder(headers, input) {
      const invalidMessage = validateDecision(input);
      return mutate(deps, headers, {
        tenantId: input.tenantId,
        orderId: input.orderId,
        path: decisionPath(input.tenantId, input.orderId),
        body: {
          decision: input.decision,
          reason: input.reason.trim(),
          expectedRevision: input.expectedRevision,
        },
        invalidMessage,
      });
    },

    async recordPlacementEvidence(headers, input) {
      const invalidMessage = validateEvidence(input);
      return mutate(deps, headers, {
        tenantId: input.tenantId,
        orderId: input.orderId,
        path: evidencePath(input.tenantId, input.orderId),
        body: {
          placementId: input.placementId,
          evidenceReference: input.evidenceReference.trim(),
          expectedRevision: input.expectedRevision,
        },
        invalidMessage,
      });
    },
  };
}

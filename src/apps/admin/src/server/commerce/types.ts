import type { components, paths } from "./api.generated";
import type { AdminOrderView } from "./validate";

/**
 * Server-only commerce types for the Admin console.
 *
 * DTOs are aliased from the generated, contract-derived client. Nothing in
 * this module may be imported by a browser component that sends or receives
 * gateway data; views receive already-projected values.
 */
export type AdminOrder = components["schemas"]["AdminOrder"];
export type AdminOrderPage = components["schemas"]["AdminOrderPage"];

export type OrderRow = {
  id: string;
  href: string;
  tier: string;
  status: string;
  total: string;
};

export const ADMIN_ORDERS_PATH =
  "/v1/admin/commerce/tenants/{tenantId}/orders" satisfies keyof paths;
export const ADMIN_ORDER_PATH =
  "/v1/admin/commerce/tenants/{tenantId}/orders/{orderId}" satisfies keyof paths;
export const ADMIN_ORDER_DECISION_PATH =
  "/v1/admin/commerce/tenants/{tenantId}/orders/{orderId}/decision" satisfies keyof paths;
export const ADMIN_PLACEMENT_EVIDENCE_PATH =
  "/v1/admin/commerce/tenants/{tenantId}/orders/{orderId}/placement-evidence" satisfies keyof paths;

/** The list endpoint always reads one fixed page size. */
export const ORDERS_PAGE_LIMIT = 25;
export const MAX_CURSOR_LENGTH = 512;
export const MAX_REASON_LENGTH = 512;
export const MAX_EVIDENCE_REFERENCE_LENGTH = 512;
export const MAX_REVISION = 4_294_967_295;

/**
 * Keycloak organization UUIDs are canonical lowercase values. The gateway
 * applies the same shape, so a malformed route value must never be fetched.
 */
export const UUID_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/u;

export type FetchLike = (input: string, init: RequestInit) => Promise<Response>;

export type CommerceDeps = {
  gatewayUrl: string;
  fetch: FetchLike;
  getAccessToken: (headers: Headers) => Promise<string | null>;
  getScope: (headers: Headers) => Promise<string | null>;
};

export type OrdersLoadResult =
  | {
      kind: "ok";
      tenantId: string;
      cursor: string | null;
      items: AdminOrderView[];
      nextCursor: string | null;
      scope: string;
      baseHref: string;
    }
  | { kind: "selectTenant" }
  | { kind: "invalidTenant"; message: string }
  | { kind: "cursorRejected"; message: string; firstPageHref: string }
  | { kind: "unauthorized" }
  | { kind: "forbidden" }
  | { kind: "notFound"; message: string }
  | { kind: "unavailable" }
  | { kind: "error"; message: string };

export type OrderLoadResult =
  | { kind: "ok"; tenantId: string; orderId: string; order: AdminOrder }
  | { kind: "invalid"; message: string }
  | { kind: "unauthorized" }
  | { kind: "forbidden" }
  | { kind: "notFound" }
  | { kind: "unavailable" }
  | { kind: "error"; message: string };

export type MutationConflictCode = "stale_revision" | "invalid_transition";

export type MutationResult =
  | { kind: "updated"; order: AdminOrderView }
  | { kind: "conflict"; code: MutationConflictCode; order: AdminOrderView | null }
  | { kind: "invalid"; message: string }
  | { kind: "unauthorized" }
  | { kind: "forbidden" }
  | { kind: "notFound" }
  | { kind: "unavailable" }
  | { kind: "error"; message: string };

export type DecisionInput = {
  decision: "approve" | "reject";
  reason: string;
  expectedRevision: number;
};

export type PlacementEvidenceInput = {
  placementId: string;
  evidenceReference: string;
  expectedRevision: number;
};

export const MESSAGES = {
  selectTenant: "Select one tenant to review its commerce orders.",
  invalidTenant: "Enter the tenant UUID exactly as issued by Keycloak (lowercase hexadecimal).",
  rejectedCursor: "That page link is no longer valid. Start again from the first page.",
  unauthorized: "Your session has expired. Sign in again to continue.",
  forbidden: "You do not have permission to view this commerce data.",
  unavailable: "Commerce is temporarily unavailable. Try again shortly.",
  genericError: "We could not load this commerce data.",
  notFound: "This order is not available for the selected tenant.",
  tenantNotFound: "This tenant is not available to your staff account.",
  invalidOrder: "This order link is not valid.",
} as const;

/**
 * ISO 4217 minor units do not always use hundredths: JPY has no fraction and
 * KWD has three decimals. Resolve the exponent through Intl, then split the
 * amount with BigInt so the minor-to-major ratio stays exact up to the
 * contract's safe-integer bound instead of accumulating binary float error.
 */
function formatMinorUnits(amountMinor: number, currency: string): string {
  const formatter = new Intl.NumberFormat("en-US", {
    style: "currency",
    currency,
  });
  const fractionDigits = formatter.resolvedOptions().maximumFractionDigits ?? 2;
  const scale = 10n ** BigInt(fractionDigits);
  const minor = BigInt(amountMinor);
  const fraction = (minor < 0n ? -minor : minor) % scale;

  return formatter
    .formatToParts(Number(minor / scale))
    .map((part) =>
      part.type === "fraction"
        ? { ...part, value: fraction.toString().padStart(fractionDigits, "0") }
        : part,
    )
    .map((part) => part.value)
    .join("");
}

export function formatMoney(total: AdminOrder["total"]): string {
  try {
    return formatMinorUnits(total.amountMinor, total.currency);
  } catch {
    return `${(total.amountMinor / 100).toFixed(2)} ${total.currency}`;
  }
}

export function formatTimestamp(value: string | null): string {
  if (value === null) return "Not recorded";
  const date = new Date(value);
  if (Number.isNaN(date.valueOf())) return value;
  return new Intl.DateTimeFormat("en-GB", {
    dateStyle: "medium",
    timeStyle: "short",
    timeZone: "UTC",
  }).format(date);
}

export function toOrderRows(items: AdminOrderView[], tenantId: string): OrderRow[] {
  return items.map((order) => ({
    id: order.orderId,
    href: `/orders/${tenantId}/${order.orderId}`,
    tier: order.tier,
    status: order.status,
    total: formatMoney(order.total),
  }));
}

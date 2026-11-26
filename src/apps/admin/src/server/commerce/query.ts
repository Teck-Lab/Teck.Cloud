import { MAX_CURSOR_LENGTH, UUID_PATTERN } from "./types";

export type OrderSearchParams = Record<string, string | string[] | undefined>;

export type OrdersQuery =
  | { kind: "noTenant" }
  | { kind: "invalidTenant"; attemptedTenantId: string }
  | { kind: "rejectedCursor"; tenantId: string; attemptedCursor: string }
  | { kind: "ok"; tenantId: string; cursor: string | null };

function singleValue(value: string | string[] | undefined): string | null | undefined {
  if (value === undefined) return undefined;
  return typeof value === "string" ? value : null;
}

/**
 * Reads the tenant and cursor only from the current request URL. A missing
 * tenant deliberately discards any cursor so a tenant switch always starts at
 * the first page, even when an old link or session history is stale.
 */
export function readOrdersQuery(params: OrderSearchParams): OrdersQuery {
  const tenant = singleValue(params.tenantId);
  if (tenant === undefined || tenant === "") return { kind: "noTenant" };
  if (tenant === null || !UUID_PATTERN.test(tenant)) {
    return { kind: "invalidTenant", attemptedTenantId: tenant ?? "" };
  }

  const cursor = singleValue(params.cursor);
  if (cursor === undefined || cursor === "") {
    return { kind: "ok", tenantId: tenant, cursor: null };
  }
  if (cursor === null || cursor.length > MAX_CURSOR_LENGTH) {
    return { kind: "rejectedCursor", tenantId: tenant, attemptedCursor: "" };
  }
  return { kind: "ok", tenantId: tenant, cursor };
}

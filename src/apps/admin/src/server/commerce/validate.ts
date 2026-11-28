import { type AdminOrder, MAX_CURSOR_LENGTH, UUID_PATTERN } from "./types";

/**
 * Safe view of one admin order. Only the fields this console renders are
 * copied out of the gateway body, including only the known nested fields, so
 * additive response properties such as a license envelope or provider
 * identifiers never reach page props.
 *
 * Enum strings are deliberately widened to `string`: commerce v1 is additive,
 * so an unknown status value must render neutrally instead of causing the
 * whole page to be discarded. Mutation controls narrow opt-in separately.
 */
export type AdminOrderView = {
  orderId: string;
  subscriptionId: string;
  tier: AdminOrder["tier"];
  addOnCodes: string[];
  total: { amountMinor: number; currency: string };
  status: string;
  revision: number;
  createdAt: string;
  updatedAt: string;
  payment: { status: string; paidThrough: string | null };
  approval: {
    status: string;
    actorId: string | null;
    reason: string | null;
    occurredAt: string | null;
  };
  fulfillment: {
    status: string;
    placementId: string | null;
    evidenceReference: string | null;
    observedAt: string | null;
    failureCode: string | null;
  };
  audit: {
    orderId: string;
    actorId: string;
    action: string;
    reason: string | null;
    occurredAt: string;
  }[];
};

export type AdminOrderPageView = {
  items: AdminOrderView[];
  nextCursor: string | null;
};

const CURRENCY_PATTERN = /^[A-Z]{3}$/u;
const STATUS_PATTERN = /^[A-Za-z][A-Za-z0-9_-]{0,63}$/u;
const MAX_ADD_ON_CODE_LENGTH = 64;
const MAX_AUDIT_ENTRIES = 200;
const MAX_PAGE_ITEMS = 100;

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function isNonEmptyString(value: unknown, maxLength: number): value is string {
  return typeof value === "string" && value.length > 0 && value.length <= maxLength;
}

function isTimestamp(value: unknown): value is string {
  return isNonEmptyString(value, 512);
}

function isRevision(value: unknown): value is number {
  return (
    typeof value === "number" && Number.isInteger(value) && value >= 1 && value <= 4_294_967_295
  );
}

/** A bounded identifier token; unknown additive enum values still render. */
function isStatusToken(value: unknown): value is string {
  return typeof value === "string" && STATUS_PATTERN.test(value);
}

function readNullableTimestamp(value: unknown): string | null | undefined {
  if (value === null) return null;
  return isTimestamp(value) ? value : undefined;
}

function readNullableString(value: unknown, maxLength: number): string | null | undefined {
  if (value === null) return null;
  return isNonEmptyString(value, maxLength) ? value : undefined;
}

function readMoney(value: unknown): AdminOrderView["total"] | null {
  if (!isRecord(value)) return null;
  const { amountMinor, currency } = value;
  if (
    typeof amountMinor !== "number" ||
    !Number.isInteger(amountMinor) ||
    amountMinor < 1 ||
    amountMinor > Number.MAX_SAFE_INTEGER
  ) {
    return null;
  }
  if (typeof currency !== "string" || !CURRENCY_PATTERN.test(currency)) return null;
  return { amountMinor, currency };
}

function readPayment(value: unknown): AdminOrderView["payment"] | null {
  if (!isRecord(value)) return null;
  const status = isStatusToken(value.status) ? value.status : null;
  const paidThrough = readNullableTimestamp(value.paidThrough);
  if (status === null || paidThrough === undefined) return null;
  return { status, paidThrough };
}

function readApproval(value: unknown): AdminOrderView["approval"] | null {
  if (!isRecord(value)) return null;
  const status = isStatusToken(value.status) ? value.status : null;
  const actorId = readNullableString(value.actorId, 128);
  const reason = readNullableString(value.reason, 512);
  const occurredAt = readNullableTimestamp(value.occurredAt);
  if (
    status === null ||
    actorId === undefined ||
    reason === undefined ||
    occurredAt === undefined
  ) {
    return null;
  }
  return { status, actorId, reason, occurredAt };
}

function readFulfillment(value: unknown): AdminOrderView["fulfillment"] | null {
  if (!isRecord(value)) return null;
  const status = isStatusToken(value.status) ? value.status : null;
  const placementId =
    value.placementId === null ? null : isUuid(value.placementId) ? value.placementId : undefined;
  const evidenceReference = readNullableString(value.evidenceReference, 512);
  const observedAt = readNullableTimestamp(value.observedAt);
  const failureCode =
    value.failureCode === null
      ? null
      : isStatusToken(value.failureCode)
        ? value.failureCode
        : undefined;
  if (
    status === null ||
    placementId === undefined ||
    evidenceReference === undefined ||
    observedAt === undefined ||
    failureCode === undefined
  ) {
    return null;
  }
  return { status, placementId, evidenceReference, observedAt, failureCode };
}

function readAuditEntry(value: unknown): AdminOrderView["audit"][number] | null {
  if (!isRecord(value)) return null;
  const orderId = value.orderId;
  const actorId = isNonEmptyString(value.actorId, 128) ? value.actorId : null;
  const action = isStatusToken(value.action) ? value.action : null;
  const reason = readNullableString(value.reason, 512);
  const occurredAt = isTimestamp(value.occurredAt) ? value.occurredAt : null;
  if (
    !isUuid(orderId) ||
    actorId === null ||
    action === null ||
    reason === undefined ||
    occurredAt === null
  ) {
    return null;
  }
  return { orderId, actorId, action, reason, occurredAt };
}

function readAudit(value: unknown): AdminOrderView["audit"] | null {
  if (!Array.isArray(value) || value.length > MAX_AUDIT_ENTRIES) return null;
  const entries: AdminOrderView["audit"] = [];
  for (const entry of value) {
    const parsed = readAuditEntry(entry);
    if (parsed === null) return null;
    entries.push(parsed);
  }
  return entries;
}

function readAddOnCodes(value: unknown): string[] | null {
  if (!Array.isArray(value)) return null;
  const codes: string[] = [];
  for (const code of value) {
    if (!isNonEmptyString(code, MAX_ADD_ON_CODE_LENGTH)) return null;
    codes.push(code);
  }
  return codes;
}

/** Validates and projects one gateway order, or returns null for a foreign shape. */
export function readAdminOrderView(value: unknown): AdminOrderView | null {
  if (!isRecord(value)) return null;
  const tier = value.tier;
  if (tier !== "shared" && tier !== "dedicated") return null;

  const orderId = value.orderId;
  const subscriptionId = value.subscriptionId;
  const status = value.status;
  const revision = value.revision;
  const createdAt = value.createdAt;
  const updatedAt = value.updatedAt;
  if (
    !isUuid(orderId) ||
    !isUuid(subscriptionId) ||
    !isStatusToken(status) ||
    !isRevision(revision) ||
    !isTimestamp(createdAt) ||
    !isTimestamp(updatedAt)
  ) {
    return null;
  }

  const addOnCodes = readAddOnCodes(value.addOnCodes);
  const total = readMoney(value.total);
  const payment = readPayment(value.payment);
  const approval = readApproval(value.approval);
  const fulfillment = readFulfillment(value.fulfillment);
  const audit = readAudit(value.audit);
  if (
    addOnCodes === null ||
    total === null ||
    payment === null ||
    approval === null ||
    fulfillment === null ||
    audit === null
  ) {
    return null;
  }

  return {
    orderId,
    subscriptionId,
    tier,
    addOnCodes,
    total,
    status,
    revision,
    createdAt,
    updatedAt,
    payment,
    approval,
    fulfillment,
    audit,
  };
}

export function readAdminOrderPageView(value: unknown): AdminOrderPageView | null {
  if (!isRecord(value)) return null;
  if (!Array.isArray(value.items) || value.items.length > MAX_PAGE_ITEMS) return null;
  const items: AdminOrderView[] = [];
  for (const item of value.items) {
    const parsed = readAdminOrderView(item);
    if (parsed === null) return null;
    items.push(parsed);
  }
  const nextCursor = value.nextCursor;
  if (nextCursor === null) return { items, nextCursor };
  if (!isCursor(nextCursor)) return null;
  return { items, nextCursor };
}

export function isUuid(value: unknown): value is string {
  return typeof value === "string" && UUID_PATTERN.test(value);
}

export function isCursor(value: unknown): value is string {
  return isNonEmptyString(value, MAX_CURSOR_LENGTH);
}

export function problemCode(value: unknown): string | null {
  return isRecord(value) && typeof value.code === "string" ? value.code : null;
}

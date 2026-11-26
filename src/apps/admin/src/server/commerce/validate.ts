import { type AdminOrder, type AdminOrderPage, MAX_CURSOR_LENGTH, UUID_PATTERN } from "./types";

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function isNonEmptyString(value: unknown, maxLength: number): value is string {
  return typeof value === "string" && value.length > 0 && value.length <= maxLength;
}

function isNullableString(value: unknown, maxLength: number): value is string | null {
  return value === null || isNonEmptyString(value, maxLength);
}

function isTimestamp(value: unknown): value is string {
  return isNonEmptyString(value, 512);
}

function isRevision(value: unknown): value is number {
  return (
    typeof value === "number" && Number.isInteger(value) && value >= 1 && value <= 4_294_967_295
  );
}

function isMoney(value: unknown): value is AdminOrder["total"] {
  return (
    isRecord(value) &&
    typeof value.amountMinor === "number" &&
    Number.isInteger(value.amountMinor) &&
    value.amountMinor > 0 &&
    typeof value.currency === "string" &&
    /^[A-Z]{3}$/u.test(value.currency)
  );
}

function isPayment(value: unknown): value is AdminOrder["payment"] {
  return (
    isRecord(value) &&
    (value.status === "pending" || value.status === "paid") &&
    (value.paidThrough === null || isTimestamp(value.paidThrough))
  );
}

function isApproval(value: unknown): value is AdminOrder["approval"] {
  return (
    isRecord(value) &&
    (value.status === "pending" || value.status === "approved" || value.status === "rejected") &&
    (value.actorId === null || isNonEmptyString(value.actorId, 128)) &&
    (value.reason === null || isNonEmptyString(value.reason, 512)) &&
    (value.occurredAt === null || isTimestamp(value.occurredAt))
  );
}

function isFulfillment(value: unknown): value is AdminOrder["fulfillment"] {
  return (
    isRecord(value) &&
    (value.status === "awaitingEvidence" ||
      value.status === "verifying" ||
      value.status === "ready" ||
      value.status === "failed") &&
    (value.placementId === null ||
      (typeof value.placementId === "string" && UUID_PATTERN.test(value.placementId))) &&
    isNullableString(value.evidenceReference, 512) &&
    (value.observedAt === null || isTimestamp(value.observedAt)) &&
    (value.failureCode === null ||
      value.failureCode === "binding_mismatch" ||
      value.failureCode === "capability_unavailable" ||
      value.failureCode === "probe_unavailable")
  );
}

function isAuditEntry(value: unknown): boolean {
  return (
    isRecord(value) &&
    typeof value.orderId === "string" &&
    UUID_PATTERN.test(value.orderId) &&
    isNonEmptyString(value.actorId, 128) &&
    (value.action === "approve" ||
      value.action === "reject" ||
      value.action === "recordPlacementEvidence") &&
    isNullableString(value.reason, 512) &&
    isTimestamp(value.occurredAt)
  );
}

export function isUuid(value: unknown): value is string {
  return typeof value === "string" && UUID_PATTERN.test(value);
}

export function isCursor(value: unknown): value is string {
  return isNonEmptyString(value, MAX_CURSOR_LENGTH);
}

export function isAdminOrder(value: unknown): value is AdminOrder {
  return (
    isRecord(value) &&
    isUuid(value.orderId) &&
    isUuid(value.subscriptionId) &&
    (value.tier === "shared" || value.tier === "dedicated") &&
    Array.isArray(value.addOnCodes) &&
    value.addOnCodes.every((code: unknown) => isNonEmptyString(code, 64)) &&
    isMoney(value.total) &&
    isNonEmptyString(value.status, 64) &&
    isRevision(value.revision) &&
    isTimestamp(value.createdAt) &&
    isTimestamp(value.updatedAt) &&
    isPayment(value.payment) &&
    isApproval(value.approval) &&
    isFulfillment(value.fulfillment) &&
    Array.isArray(value.audit) &&
    value.audit.length <= 200 &&
    value.audit.every(isAuditEntry)
  );
}

export function isAdminOrderPage(value: unknown): value is AdminOrderPage {
  return (
    isRecord(value) &&
    Array.isArray(value.items) &&
    value.items.length <= 100 &&
    value.items.every(isAdminOrder) &&
    (value.nextCursor === null || isCursor(value.nextCursor))
  );
}

export function problemCode(value: unknown): string | null {
  return isRecord(value) && typeof value.code === "string" ? value.code : null;
}

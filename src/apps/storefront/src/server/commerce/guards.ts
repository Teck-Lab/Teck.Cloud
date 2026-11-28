import type { components } from "./api.generated";

/**
 * Canonical runtime guards for the fixed customer commerce v1 contract.
 * Generated OpenAPI types describe the accepted shape; these checks stop a
 * malformed gateway body from crossing into page props or a checkout redirect.
 */

type CatalogResponse = components["schemas"]["CatalogResponse"];
type Basket = components["schemas"]["Basket"];
type CustomerOrder = components["schemas"]["CustomerOrder"];
type OrderPage = components["schemas"]["OrderPage"];
type CreateOrderResponse = components["schemas"]["CreateOrderResponse"];

const UUID_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/;
const ADD_ON_CODE_PATTERN = /^[A-Za-z][A-Za-z0-9_-]{0,63}$/;
const CURRENCY_PATTERN = /^[A-Z]{3}$/;
const UINT32_MAX = 4294967295;

export const KNOWN_ORDER_STATUSES: Readonly<Record<string, true>> = {
  pendingPayment: true,
  pendingApproval: true,
  pendingReadiness: true,
  active: true,
  rejected: true,
  failed: true,
};

export function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function isSafeIntegerInRange(value: unknown, minimum: number, maximum: number): value is number {
  return (
    typeof value === "number" && Number.isSafeInteger(value) && value >= minimum && value <= maximum
  );
}

function isBoundedString(value: unknown, minimum: number, maximum: number): value is string {
  return typeof value === "string" && value.length >= minimum && value.length <= maximum;
}

function isDateTime(value: unknown): value is string {
  return typeof value === "string" && value.length > 0 && !Number.isNaN(Date.parse(value));
}

function readMoney(value: unknown): boolean {
  if (!isRecord(value)) {
    return false;
  }
  return (
    isSafeIntegerInRange(value.amountMinor, 1, Number.MAX_SAFE_INTEGER) &&
    typeof value.currency === "string" &&
    CURRENCY_PATTERN.test(value.currency)
  );
}

function readAddOn(value: unknown): boolean {
  if (!isRecord(value)) {
    return false;
  }
  return (
    typeof value.code === "string" &&
    ADD_ON_CODE_PATTERN.test(value.code) &&
    isBoundedString(value.displayName, 1, 120) &&
    readMoney(value.price)
  );
}

function readOffer(value: unknown): boolean {
  if (!isRecord(value)) {
    return false;
  }
  if (
    !isBoundedString(value.offerId, 1, 64) ||
    !isBoundedString(value.tier, 1, 32) ||
    !isBoundedString(value.displayName, 1, 120) ||
    !readMoney(value.basePrice) ||
    !Array.isArray(value.addOns) ||
    value.addOns.length > 20
  ) {
    return false;
  }
  return value.addOns.every((addOn) => readAddOn(addOn));
}

function readAddOnCodes(value: unknown): boolean {
  if (!Array.isArray(value) || value.length > 20) {
    return false;
  }
  const seen = new Set<string>();
  for (const code of value) {
    if (typeof code !== "string" || !ADD_ON_CODE_PATTERN.test(code)) {
      return false;
    }
    if (seen.has(code)) {
      return false;
    }
    seen.add(code);
  }
  return true;
}

function readSelection(value: unknown): boolean {
  if (value === null) {
    return true;
  }
  if (!isRecord(value)) {
    return false;
  }
  return isBoundedString(value.offerId, 1, 64) && readAddOnCodes(value.addOnCodes);
}

function readLicense(value: unknown): boolean {
  if (value === null) {
    return true;
  }
  if (!isRecord(value)) {
    return false;
  }
  return (
    isBoundedString(value.document, 1, 16384) &&
    isBoundedString(value.keyId, 1, 64) &&
    isSafeIntegerInRange(value.revision, 1, UINT32_MAX) &&
    isDateTime(value.expiresAt)
  );
}

function readCustomerOrderShape(value: unknown): boolean {
  if (!isRecord(value)) {
    return false;
  }
  return (
    typeof value.orderId === "string" &&
    UUID_PATTERN.test(value.orderId) &&
    typeof value.subscriptionId === "string" &&
    UUID_PATTERN.test(value.subscriptionId) &&
    isBoundedString(value.tier, 1, 32) &&
    readAddOnCodes(value.addOnCodes) &&
    readMoney(value.total) &&
    isBoundedString(value.status, 1, 64) &&
    isSafeIntegerInRange(value.revision, 1, UINT32_MAX) &&
    isDateTime(value.createdAt) &&
    isDateTime(value.updatedAt) &&
    readLicense(value.license)
  );
}

export function readCatalogResponse(value: unknown): CatalogResponse | null {
  if (!isRecord(value)) {
    return null;
  }
  if (!Array.isArray(value.offers) || value.offers.length > 20) {
    return null;
  }
  if (!value.offers.every((offer) => readOffer(offer))) {
    return null;
  }
  return value as CatalogResponse;
}

export function readBasket(value: unknown): Basket | null {
  if (!isRecord(value)) {
    return null;
  }
  if (
    !readSelection(value.selection) ||
    !isSafeIntegerInRange(value.revision, 0, UINT32_MAX) ||
    !(value.total === null || readMoney(value.total))
  ) {
    return null;
  }
  return value as Basket;
}

export function readCustomerOrder(value: unknown): CustomerOrder | null {
  return readCustomerOrderShape(value) ? (value as CustomerOrder) : null;
}

export function readOrderPage(value: unknown): OrderPage | null {
  if (!isRecord(value)) {
    return null;
  }
  if (!Array.isArray(value.items) || value.items.length > 100) {
    return null;
  }
  if (!value.items.every((item) => readCustomerOrderShape(item))) {
    return null;
  }
  if (value.nextCursor !== null && !isBoundedString(value.nextCursor, 1, 512)) {
    return null;
  }
  return value as OrderPage;
}

export function readCreateOrderResponse(value: unknown): CreateOrderResponse | null {
  if (!isRecord(value)) {
    return null;
  }
  if (!readCustomerOrderShape(value.order)) {
    return null;
  }
  const order = value.order as { status: string };
  if (KNOWN_ORDER_STATUSES[order.status] !== true) {
    return null;
  }
  if (value.checkoutUrl !== null && !isBoundedString(value.checkoutUrl, 1, 4096)) {
    return null;
  }
  return value as CreateOrderResponse;
}

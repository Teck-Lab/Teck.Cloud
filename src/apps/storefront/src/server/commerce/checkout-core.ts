import type { CheckoutResult } from "@/checkout/types";
import type { components } from "./api.generated";
import { KNOWN_ORDER_STATUSES } from "./guards";
import {
  CommerceHttpError,
  CommerceInvalidResponseError,
  CommerceUnavailableError,
} from "./problems";

type CreateOrderResponse = components["schemas"]["CreateOrderResponse"];

const UUID_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/;
const UINT32_MAX = 4294967295;

export type CheckoutSubmission = {
  scope: string;
  basketRevision: number;
  idempotencyKey: string;
};

export type CheckoutOrigin = {
  origin: string;
  allowSimulator: boolean;
};

export type CheckoutDeps = {
  identity: { accessToken: string; scope: string } | null;
  createOrder: (input: {
    accessToken: string;
    basketRevision: number;
    idempotencyKey: string;
  }) => Promise<CreateOrderResponse>;
  checkoutOrigin: CheckoutOrigin | null;
};

/**
 * Reads the single configured hosted-checkout origin. Production only accepts
 * an exact HTTPS origin; an isolated local simulator may use loopback HTTP when
 * the process is not a production build.
 */
export function readCheckoutOrigin(
  env: Record<string, string | undefined> = process.env,
): CheckoutOrigin | null {
  const raw = env.CHECKOUT_ORIGIN?.trim() ?? "";
  if (raw.length === 0) {
    return null;
  }

  let url: URL;
  try {
    url = new URL(raw);
  } catch {
    return null;
  }

  if (
    url.username.length > 0 ||
    url.password.length > 0 ||
    url.search.length > 0 ||
    url.hash.length > 0 ||
    url.pathname !== "/"
  ) {
    return null;
  }

  if (url.protocol === "https:") {
    return { origin: url.origin, allowSimulator: false };
  }

  const loopback =
    url.hostname === "localhost" ||
    url.hostname === "127.0.0.1" ||
    url.hostname === "[::1]" ||
    url.hostname === "::1";
  if (url.protocol === "http:" && loopback && env.NODE_ENV !== "production") {
    return { origin: url.origin, allowSimulator: true };
  }

  return null;
}

export function validateCheckoutSubmission(value: unknown): CheckoutSubmission | null {
  if (typeof value !== "object" || value === null || Array.isArray(value)) {
    return null;
  }
  const input = value as {
    scope?: unknown;
    basketRevision?: unknown;
    idempotencyKey?: unknown;
  };

  if (typeof input.scope !== "string" || input.scope.length === 0 || input.scope.length > 512) {
    return null;
  }
  if (
    typeof input.basketRevision !== "number" ||
    !Number.isSafeInteger(input.basketRevision) ||
    input.basketRevision < 1 ||
    input.basketRevision > UINT32_MAX
  ) {
    return null;
  }
  if (typeof input.idempotencyKey !== "string" || !UUID_PATTERN.test(input.idempotencyKey)) {
    return null;
  }

  return {
    scope: input.scope,
    basketRevision: input.basketRevision,
    idempotencyKey: input.idempotencyKey,
  };
}

function resolveCheckoutUrl(raw: string, configured: CheckoutOrigin): string | null {
  let url: URL;
  try {
    url = new URL(raw);
  } catch {
    return null;
  }

  if (url.username.length > 0 || url.password.length > 0) {
    return null;
  }
  if (url.origin !== configured.origin) {
    return null;
  }
  if (url.protocol === "https:") {
    return url.href;
  }
  if (url.protocol === "http:" && configured.allowSimulator) {
    return url.href;
  }
  return null;
}

export async function performCheckout(deps: CheckoutDeps, value: unknown): Promise<CheckoutResult> {
  const input = validateCheckoutSubmission(value);
  if (input === null) {
    return { status: 400, code: "invalid_request" };
  }

  if (deps.identity === null || deps.identity.accessToken.trim().length === 0) {
    return { status: 401, code: "unauthenticated" };
  }

  if (deps.identity.scope !== input.scope) {
    return { status: 403, code: "forbidden" };
  }

  let response: CreateOrderResponse;
  try {
    response = await deps.createOrder({
      accessToken: deps.identity.accessToken,
      basketRevision: input.basketRevision,
      idempotencyKey: input.idempotencyKey,
    });
  } catch (error) {
    if (error instanceof CommerceHttpError) {
      if (error.problem === null) {
        return { status: 502 };
      }
      return { status: error.problem.status, code: error.problem.code };
    }
    if (error instanceof CommerceInvalidResponseError) {
      return { status: 502 };
    }
    if (error instanceof CommerceUnavailableError) {
      return { status: 503, code: "dependency_unavailable" };
    }
    return { status: 503, code: "dependency_unavailable" };
  }

  const orderId = response.order.orderId;
  const orderStatus = response.order.status;
  if (
    !UUID_PATTERN.test(orderId) ||
    KNOWN_ORDER_STATUSES[orderStatus] !== true ||
    (typeof response.checkoutUrl !== "string" && response.checkoutUrl !== null)
  ) {
    return { status: 502 };
  }

  const order = { id: orderId, status: orderStatus };
  if (orderStatus !== "pendingPayment") {
    return { status: 201, order, checkoutUrl: null };
  }

  if (response.checkoutUrl === null) {
    return { status: 201, order, checkoutUrl: null };
  }

  const checkoutUrl =
    deps.checkoutOrigin === null
      ? null
      : resolveCheckoutUrl(response.checkoutUrl, deps.checkoutOrigin);
  if (checkoutUrl === null) {
    return { status: 201, order, checkoutUrl: null, checkoutDenied: true };
  }

  return { status: 201, order, checkoutUrl };
}

import { describe, expect, test } from "bun:test";
import type { components } from "./api.generated";
import {
  type CheckoutDeps,
  performCheckout,
  readCheckoutOrigin,
  validateCheckoutSubmission,
} from "./checkout-core";
import {
  CommerceHttpError,
  CommerceInvalidResponseError,
  CommerceUnavailableError,
} from "./problems";

type CreateOrderResponse = components["schemas"]["CreateOrderResponse"];

const ORDER_ID = "3a4b5c6d-7e8f-4a2b-9c3d-4e5f60718293";
const SUBSCRIPTION_ID = "5b6c7d8e-9fa0-4b3c-8d4e-5f60718293a4";
const KEY = `a${crypto.randomUUID().slice(1)}`;
const HTTPS_ORIGIN = { origin: "https://checkout.stripe.com", allowSimulator: false };

const input = {
  scope: "opaque-session-scope",
  basketRevision: 4,
  idempotencyKey: KEY,
};

function createdResponse(overrides: Partial<CreateOrderResponse> = {}): CreateOrderResponse {
  return {
    order: {
      orderId: ORDER_ID,
      subscriptionId: SUBSCRIPTION_ID,
      tier: "shared",
      addOnCodes: [],
      total: { amountMinor: 1000, currency: "USD" },
      status: "pendingPayment",
      revision: 1,
      createdAt: "2026-11-23T19:10:00Z",
      updatedAt: "2026-11-23T19:10:00Z",
      license: null,
    },
    checkoutUrl: null,
    ...overrides,
  } as CreateOrderResponse;
}

function harness(overrides: Partial<CheckoutDeps> = {}) {
  const calls: {
    accessToken: string;
    basketRevision: number;
    idempotencyKey: string;
  }[] = [];
  const deps: CheckoutDeps = {
    identity: { accessToken: "token-1", scope: "opaque-session-scope" },
    checkoutOrigin: HTTPS_ORIGIN,
    createOrder: async (orderInput) => {
      calls.push(orderInput);
      return createdResponse();
    },
    ...overrides,
  };
  return { deps, calls };
}

describe("checkout submission", () => {
  test("rejects a malformed intent before authenticating or calling the gateway", async () => {
    const { deps, calls } = harness();

    const result = await performCheckout(deps, {
      scope: "",
      basketRevision: 0,
      idempotencyKey: "not-a-key",
    });

    expect(result).toEqual({ status: 400, code: "invalid_request" });
    expect(calls).toHaveLength(0);
  });

  test("requires an authenticated session and makes no gateway call", async () => {
    const { deps, calls } = harness({ identity: null });

    const result = await performCheckout(deps, input);

    expect(result).toEqual({ status: 401, code: "unauthenticated" });
    expect(calls).toHaveLength(0);
  });

  test("rejects a stale browser scope with 403 before any gateway call", async () => {
    const { deps, calls } = harness();

    const result = await performCheckout(deps, {
      ...input,
      scope: "another-session-scope",
    });

    expect(result).toEqual({ status: 403, code: "forbidden" });
    expect(calls).toHaveLength(0);
  });

  test("re-derives the scope on every call", async () => {
    const first = harness();
    expect(await performCheckout(first.deps, input)).toMatchObject({
      status: 201,
    });

    const second = harness({
      identity: { accessToken: "token-1", scope: "rotated-scope" },
    });
    expect(await performCheckout(second.deps, input)).toEqual({
      status: 403,
      code: "forbidden",
    });
    expect(second.calls).toHaveLength(0);
  });

  test("passes only the original key and revision to the gateway", async () => {
    const { deps, calls } = harness();

    const result = await performCheckout(deps, input);

    expect(result).toEqual({
      status: 201,
      order: { id: ORDER_ID, status: "pendingPayment" },
      checkoutUrl: null,
    });
    expect(calls).toEqual([
      {
        accessToken: "token-1",
        basketRevision: 4,
        idempotencyKey: KEY,
      },
    ]);
  });

  test("returns an exact configured HTTPS checkout URL", async () => {
    const url = "https://checkout.stripe.com/c/pay/cs_test_123";
    const { deps } = harness({
      createOrder: async () => createdResponse({ checkoutUrl: url }),
    });

    const result = await performCheckout(deps, input);

    expect(result).toEqual({
      status: 201,
      order: { id: ORDER_ID, status: "pendingPayment" },
      checkoutUrl: url,
    });
  });

  test("denies a credential-bearing checkout URL without redirecting", async () => {
    const { deps } = harness({
      createOrder: async () =>
        createdResponse({
          checkoutUrl: "https://user:pass@checkout.stripe.com/c/pay/1",
        }),
    });

    const result = await performCheckout(deps, input);

    expect(result).toEqual({
      status: 201,
      order: { id: ORDER_ID, status: "pendingPayment" },
      checkoutUrl: null,
      checkoutDenied: true,
    });
  });

  test("denies an off-origin or downgraded checkout URL", async () => {
    for (const checkoutUrl of [
      "https://attacker.example/c/pay/1",
      "http://checkout.stripe.com/c/pay/1",
      "https://checkout.stripe.com.evil.example/c/pay/1",
    ]) {
      const { deps } = harness({
        createOrder: async () => createdResponse({ checkoutUrl }),
      });
      const result = await performCheckout(deps, input);
      expect(result).toMatchObject({ status: 201, checkoutDenied: true });
      expect((result as { checkoutUrl?: string | null }).checkoutUrl).toBeNull();
    }
  });

  test("allows the explicit isolated HTTP simulator origin only when configured", async () => {
    const simulator = {
      origin: "http://127.0.0.1:12111",
      allowSimulator: true,
    };
    const simulatorUrl = "http://127.0.0.1:12111/c/pay/sim_1";
    const { deps } = harness({
      checkoutOrigin: simulator,
      createOrder: async () => createdResponse({ checkoutUrl: simulatorUrl }),
    });

    expect(await performCheckout(deps, input)).toEqual({
      status: 201,
      order: { id: ORDER_ID, status: "pendingPayment" },
      checkoutUrl: simulatorUrl,
    });

    const httpsOnly = harness({
      checkoutOrigin: HTTPS_ORIGIN,
      createOrder: async () => createdResponse({ checkoutUrl: simulatorUrl }),
    });
    expect(await performCheckout(httpsOnly.deps, input)).toMatchObject({
      checkoutDenied: true,
    });
  });

  test("returns a null URL when checkout is not yet payable", async () => {
    const { deps } = harness({
      createOrder: async () =>
        createdResponse({
          order: {
            ...createdResponse().order,
            status: "active",
          },
          checkoutUrl: "https://checkout.stripe.com/c/pay/1",
        }),
    });

    expect(await performCheckout(deps, input)).toEqual({
      status: 201,
      order: { id: ORDER_ID, status: "active" },
      checkoutUrl: null,
    });
  });

  test("rejects malformed order ids, statuses and URL fields", async () => {
    const cases: CreateOrderResponse[] = [
      createdResponse({
        order: { ...createdResponse().order, orderId: "not-a-uuid" },
      }),
      createdResponse({
        order: { ...createdResponse().order, status: "settled" as never },
      }),
      { order: createdResponse().order } as CreateOrderResponse,
    ];

    for (const response of cases) {
      const { deps } = harness({ createOrder: async () => response });
      expect(await performCheckout(deps, input)).toEqual({ status: 502 });
    }
  });

  test("passes only validated problem codes from the gateway", async () => {
    const { deps } = harness({
      createOrder: async () => {
        throw new CommerceHttpError(404, { status: 404, code: "not_found" });
      },
    });
    expect(await performCheckout(deps, input)).toEqual({
      status: 404,
      code: "not_found",
    });

    const conflict = harness({
      createOrder: async () => {
        throw new CommerceHttpError(409, {
          status: 409,
          code: "idempotency_conflict",
        });
      },
    });
    expect(await performCheckout(conflict.deps, input)).toEqual({
      status: 409,
      code: "idempotency_conflict",
    });

    const malformedProblem = harness({
      createOrder: async () => {
        throw new CommerceHttpError(404, null);
      },
    });
    expect(await performCheckout(malformedProblem.deps, input)).toEqual({
      status: 502,
    });
  });

  test("fails closed on invalid or unavailable gateway responses", async () => {
    const invalid = harness({
      createOrder: async () => {
        throw new CommerceInvalidResponseError();
      },
    });
    expect(await performCheckout(invalid.deps, input)).toEqual({ status: 502 });

    const unavailable = harness({
      createOrder: async () => {
        throw new CommerceUnavailableError();
      },
    });
    expect(await performCheckout(unavailable.deps, input)).toEqual({
      status: 503,
      code: "dependency_unavailable",
    });
  });
});

describe("configured checkout origin", () => {
  test("accepts an exact HTTPS origin", () => {
    expect(readCheckoutOrigin({ CHECKOUT_ORIGIN: "https://checkout.stripe.com" })).toEqual({
      origin: "https://checkout.stripe.com",
      allowSimulator: false,
    });
    expect(readCheckoutOrigin({ CHECKOUT_ORIGIN: "https://checkout.stripe.com/" })).toEqual({
      origin: "https://checkout.stripe.com",
      allowSimulator: false,
    });
  });

  test("accepts loopback HTTP only outside production", () => {
    expect(
      readCheckoutOrigin({
        CHECKOUT_ORIGIN: "http://127.0.0.1:12111",
        NODE_ENV: "test",
      }),
    ).toEqual({ origin: "http://127.0.0.1:12111", allowSimulator: true });
    expect(
      readCheckoutOrigin({
        CHECKOUT_ORIGIN: "http://127.0.0.1:12111",
        NODE_ENV: "production",
      }),
    ).toBeNull();
    expect(readCheckoutOrigin({ CHECKOUT_ORIGIN: "http://checkout.example" })).toBeNull();
  });

  test("rejects missing, credential, path, query and hash values", () => {
    for (const value of [
      undefined,
      "not a url",
      "https://user:pass@checkout.stripe.com",
      "https://checkout.stripe.com/pay",
      "https://checkout.stripe.com/?a=1",
      "https://checkout.stripe.com/#fragment",
    ]) {
      expect(readCheckoutOrigin({ CHECKOUT_ORIGIN: value })).toBeNull();
    }
  });
});

describe("intent validation", () => {
  test("accepts only a positive uint32 revision and a UUID key", () => {
    expect(validateCheckoutSubmission(input)).toEqual(input);
    for (const candidate of [
      null,
      [],
      { ...input, basketRevision: 0 },
      { ...input, basketRevision: 1.5 },
      { ...input, basketRevision: 4294967296 },
      { ...input, idempotencyKey: KEY.toUpperCase() },
      { ...input, scope: "x".repeat(513) },
    ]) {
      expect(validateCheckoutSubmission(candidate)).toBeNull();
    }
  });
});

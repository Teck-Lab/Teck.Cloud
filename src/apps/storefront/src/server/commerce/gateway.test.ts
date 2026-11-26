import { describe, expect, test } from "bun:test";
import { createCommerceGateway, normalizeCommerceBaseUrl } from "./gateway";
import {
  CommerceHttpError,
  CommerceInputError,
  CommerceInvalidResponseError,
  CommerceUnavailableError,
} from "./problems";

const ORDER_ID = "3a4b5c6d-7e8f-4a2b-9c3d-4e5f60718293";
const SUBSCRIPTION_ID = "5b6c7d8e-9fa0-4b3c-8d4e-5f60718293a4";
const IDEMPOTENCY_KEY = "11111111-1111-4111-8111-111111111111";

const catalog = {
  offers: [
    {
      offerId: "postgres-shared-monthly",
      tier: "shared",
      displayName: "Shared PostgreSQL",
      basePrice: { amountMinor: 1000, currency: "USD" },
      addOns: [],
    },
  ],
};

const basket = {
  selection: { offerId: "postgres-shared-monthly", addOnCodes: [] },
  revision: 2,
  total: { amountMinor: 1000, currency: "USD" },
};

const order = {
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
};

const created = { order, checkoutUrl: null };

type RecordedCall = { url: string; init: RequestInit };

function reply(body: unknown, status = 200): Response {
  return {
    ok: status >= 200 && status < 300,
    status,
    json: async () => body,
  } as Response;
}

function recordingFetch(responder: (url: string, init: RequestInit) => Response): {
  fetchImpl: typeof fetch;
  calls: RecordedCall[];
} {
  const calls: RecordedCall[] = [];
  const fetchImpl = async (input: RequestInfo | URL, init?: RequestInit): Promise<Response> => {
    const url = typeof input === "string" ? input : input.toString();
    const requestInit = init ?? {};
    calls.push({ url, init: requestInit });
    return responder(url, requestInit);
  };
  return { fetchImpl: fetchImpl as typeof fetch, calls };
}

describe("commerce gateway", () => {
  test("calls the catalog path with only a bearer token and no-store options", async () => {
    const { fetchImpl, calls } = recordingFetch(() => reply(catalog));
    const gateway = createCommerceGateway({
      baseUrl: "https://gateway.example/",
      fetchImpl,
    });

    const response = await gateway.getCatalog("token-1");

    expect(response.offers).toHaveLength(1);
    expect(calls).toHaveLength(1);
    const call = calls[0];
    expect(call?.url).toBe("https://gateway.example/v1/commerce/catalog");
    const headers = new Headers(call?.init.headers);
    expect(headers.get("authorization")).toBe("Bearer token-1");
    expect(headers.get("cookie")).toBeNull();
    expect(call?.init.cache).toBe("no-store");
    expect(call?.init.credentials).toBe("omit");
    expect(call?.init.redirect).toBe("error");
  });

  test("lists a fixed page of 25 with an encoded opaque cursor", async () => {
    const cursor = "a b/+?&=é";
    const { fetchImpl, calls } = recordingFetch(() =>
      reply({ items: [order], nextCursor: "next page" }),
    );
    const gateway = createCommerceGateway({
      baseUrl: "https://gateway.example",
      fetchImpl,
    });

    const page = await gateway.listOrders("token-1", cursor);

    expect(page.items).toHaveLength(1);
    const call = calls[0];
    const url = new URL(call?.url ?? "");
    expect(url.pathname).toBe("/v1/commerce/orders");
    expect(url.searchParams.get("limit")).toBe("25");
    expect(url.searchParams.get("cursor")).toBe(cursor);
    expect(call?.url).toContain("cursor=a+b%2F%2B%3F%26%3D%C3%A9");
    expect(call?.url).not.toContain(cursor);
  });

  test("walks from the first page through an encoded cursor to exhaustion", async () => {
    const pages = [
      { items: [order], nextCursor: "next cursor/+?" },
      { items: [order], nextCursor: null },
    ];
    let index = 0;
    const { fetchImpl, calls } = recordingFetch(() => {
      const page = pages[index] ?? pages[pages.length - 1];
      index += 1;
      return reply(page);
    });
    const gateway = createCommerceGateway({
      baseUrl: "https://gateway.example",
      fetchImpl,
    });

    const first = await gateway.listOrders("token-1", null);
    expect(first.nextCursor).toBe("next cursor/+?");
    expect(new URL(calls[0]?.url ?? "").searchParams.has("cursor")).toBe(false);

    const second = await gateway.listOrders("token-1", first.nextCursor);
    expect(second.nextCursor).toBeNull();
    expect(new URL(calls[1]?.url ?? "").searchParams.get("cursor")).toBe("next cursor/+?");

    const exhausted = await gateway.listOrders("token-1", second.nextCursor);
    expect(exhausted.nextCursor).toBeNull();
    expect(new URL(calls[2]?.url ?? "").searchParams.has("cursor")).toBe(false);
  });

  test("keeps pagination at the first page when the cursor is null", async () => {
    const { fetchImpl, calls } = recordingFetch(() => reply({ items: [], nextCursor: null }));
    const gateway = createCommerceGateway({
      baseUrl: "https://gateway.example",
      fetchImpl,
    });

    await gateway.listOrders("token-1", null);

    const url = new URL(calls[0]?.url ?? "");
    expect(url.searchParams.get("limit")).toBe("25");
    expect(url.searchParams.has("cursor")).toBe(false);
  });

  test("submits checkout with the original key and basket revision only", async () => {
    const { fetchImpl, calls } = recordingFetch(() => reply(created, 201));
    const gateway = createCommerceGateway({
      baseUrl: "https://gateway.example",
      fetchImpl,
    });

    const result = await gateway.createOrder("token-1", {
      basketRevision: 4,
      idempotencyKey: IDEMPOTENCY_KEY,
    });

    expect(result.order.orderId).toBe(ORDER_ID);
    const call = calls[0];
    expect(call?.url).toBe("https://gateway.example/v1/commerce/orders");
    expect(call?.init.method).toBe("POST");
    const headers = new Headers(call?.init.headers);
    expect(headers.get("idempotency-key")).toBe(IDEMPOTENCY_KEY);
    expect(headers.get("authorization")).toBe("Bearer token-1");
    expect(headers.get("cookie")).toBeNull();
    const body = JSON.parse(String(call?.init.body)) as Record<string, unknown>;
    expect(Object.keys(body)).toEqual(["basketRevision"]);
    expect(body.basketRevision).toBe(4);
  });

  test("sends only the selection and expected revision when saving a basket", async () => {
    const { fetchImpl, calls } = recordingFetch(() => reply({ ...basket, revision: 3 }, 200));
    const gateway = createCommerceGateway({
      baseUrl: "https://gateway.example",
      fetchImpl,
    });

    await gateway.putBasket("token-1", {
      selection: { offerId: "postgres-shared-monthly", addOnCodes: [] },
      expectedRevision: 2,
    });

    const body = JSON.parse(String(calls[0]?.init.body)) as Record<string, unknown>;
    expect(Object.keys(body).sort()).toEqual(["expectedRevision", "selection"]);
    expect(body.expectedRevision).toBe(2);
  });

  test("recognises only the Problem code allowed for its status", async () => {
    const notFound = {
      type: "https://api.teck.cloud/problems/not-found",
      title: "Resource not available",
      status: 404,
      code: "not_found",
    };
    const { fetchImpl } = recordingFetch(() => reply(notFound, 404));
    const gateway = createCommerceGateway({
      baseUrl: "https://gateway.example",
      fetchImpl,
    });

    try {
      await gateway.getOrder("token-1", ORDER_ID);
      throw new Error("expected a repository failure");
    } catch (error) {
      expect(error).toBeInstanceOf(CommerceHttpError);
      const httpError = error as CommerceHttpError;
      expect(httpError.problem?.code).toBe("not_found");
    }

    const wrongCode = { ...notFound, code: "forbidden" };
    const mismatch = recordingFetch(() => reply(wrongCode, 404));
    const second = createCommerceGateway({
      baseUrl: "https://gateway.example",
      fetchImpl: mismatch.fetchImpl,
    });
    try {
      await second.getOrder("token-1", ORDER_ID);
      throw new Error("expected a repository failure");
    } catch (error) {
      expect(error).toBeInstanceOf(CommerceHttpError);
      expect((error as CommerceHttpError).problem).toBeNull();
    }
  });

  test("maps the three checkout conflicts only through valid Problems", async () => {
    for (const code of ["stale_revision", "idempotency_conflict", "invalid_transition"] as const) {
      const problem = {
        type: `https://api.teck.cloud/problems/${code}`,
        title: "Conflict",
        status: 409,
        code,
      };
      const { fetchImpl } = recordingFetch(() => reply(problem, 409));
      const gateway = createCommerceGateway({
        baseUrl: "https://gateway.example",
        fetchImpl,
      });
      try {
        await gateway.createOrder("token-1", {
          basketRevision: 4,
          idempotencyKey: IDEMPOTENCY_KEY,
        });
        throw new Error("expected a repository failure");
      } catch (error) {
        expect((error as CommerceHttpError).problem?.code).toBe(code);
      }
    }
  });

  test("rejects a malformed success body instead of returning a partial DTO", async () => {
    const { fetchImpl } = recordingFetch(() => reply({ ...order, subscriptionId: "not-a-uuid" }));
    const gateway = createCommerceGateway({
      baseUrl: "https://gateway.example",
      fetchImpl,
    });

    await expect(gateway.getOrder("token-1", ORDER_ID)).rejects.toBeInstanceOf(
      CommerceInvalidResponseError,
    );
  });

  test("fails closed when the gateway transport fails", async () => {
    const fetchImpl = async (): Promise<Response> => {
      throw new Error("socket detail that must not escape");
    };
    const gateway = createCommerceGateway({
      baseUrl: "https://gateway.example",
      fetchImpl: fetchImpl as unknown as typeof fetch,
    });

    await expect(gateway.getBasket("token-1")).rejects.toBeInstanceOf(CommerceUnavailableError);
  });

  test("does not fetch without a token or with invalid input", async () => {
    const { fetchImpl, calls } = recordingFetch(() => reply(catalog));
    const gateway = createCommerceGateway({
      baseUrl: "https://gateway.example",
      fetchImpl,
    });

    await expect(gateway.getCatalog("  ")).rejects.toBeInstanceOf(CommerceInputError);
    await expect(gateway.getOrder("token-1", "bad-order")).rejects.toBeInstanceOf(
      CommerceInputError,
    );
    await expect(gateway.listOrders("token-1", "x".repeat(513))).rejects.toBeInstanceOf(
      CommerceInputError,
    );
    await expect(
      gateway.createOrder("token-1", {
        basketRevision: 0,
        idempotencyKey: IDEMPOTENCY_KEY,
      }),
    ).rejects.toBeInstanceOf(CommerceInputError);
    expect(calls).toHaveLength(0);
  });

  test("accepts only an HTTPS gateway origin or loopback HTTP", () => {
    expect(normalizeCommerceBaseUrl("https://gateway.example/")).toBe("https://gateway.example");
    expect(normalizeCommerceBaseUrl("http://localhost:8080")).toBe("http://localhost:8080");
    expect(() => normalizeCommerceBaseUrl("http://gateway.example")).toThrow();
    expect(() => normalizeCommerceBaseUrl("https://user:pass@gateway.example")).toThrow();
    expect(() => normalizeCommerceBaseUrl("https://gateway.example?path=1")).toThrow();
  });
});

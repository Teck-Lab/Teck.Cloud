import "server-only";

import type { BasketSaveInput } from "@/commerce/contracts";
import type { components } from "./api.generated";
import {
  readBasket,
  readCatalogResponse,
  readCreateOrderResponse,
  readCustomerOrder,
  readOrderPage,
} from "./guards";
import {
  CommerceHttpError,
  CommerceInputError,
  CommerceInvalidResponseError,
  CommerceUnavailableError,
  parseCommerceProblem,
} from "./problems";

type CatalogResponse = components["schemas"]["CatalogResponse"];
type Basket = components["schemas"]["Basket"];
type CustomerOrder = components["schemas"]["CustomerOrder"];
type OrderPage = components["schemas"]["OrderPage"];
type CreateOrderResponse = components["schemas"]["CreateOrderResponse"];

const UUID_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/;
const UINT32_MAX = 4294967295;
const FIXED_PAGE_LIMIT = 25;

export type CommerceGateway = {
  getCatalog(accessToken: string): Promise<CatalogResponse>;
  getBasket(accessToken: string): Promise<Basket>;
  putBasket(accessToken: string, input: BasketSaveInput): Promise<Basket>;
  listOrders(accessToken: string, cursor: string | null): Promise<OrderPage>;
  getOrder(accessToken: string, orderId: string): Promise<CustomerOrder>;
  createOrder(
    accessToken: string,
    input: { basketRevision: number; idempotencyKey: string },
  ): Promise<CreateOrderResponse>;
};

export type CommerceGatewayOptions = {
  baseUrl: string;
  fetchImpl?: typeof fetch;
};

export function normalizeCommerceBaseUrl(raw: string): string {
  const trimmed = raw.trim();
  if (trimmed.length === 0) {
    throw new CommerceInputError("COMMERCE_GATEWAY_URL is not configured");
  }

  let url: URL;
  try {
    url = new URL(trimmed);
  } catch {
    throw new CommerceInputError("COMMERCE_GATEWAY_URL is not a URL");
  }

  if (url.username.length > 0 || url.password.length > 0) {
    throw new CommerceInputError("COMMERCE_GATEWAY_URL must not contain credentials");
  }
  if (url.search.length > 0 || url.hash.length > 0) {
    throw new CommerceInputError("COMMERCE_GATEWAY_URL must be an origin or path");
  }

  const loopback =
    url.hostname === "localhost" ||
    url.hostname === "127.0.0.1" ||
    url.hostname === "[::1]" ||
    url.hostname === "::1";
  if (url.protocol !== "https:" && !(url.protocol === "http:" && loopback)) {
    throw new CommerceInputError(
      "COMMERCE_GATEWAY_URL must use HTTPS (loopback HTTP is allowed for local assembly)",
    );
  }

  return url.href.replace(/\/+$/, "");
}

function assertToken(accessToken: string): void {
  if (accessToken.trim().length === 0) {
    throw new CommerceInputError("A gateway access token is required");
  }
}

function assertOrderId(orderId: string): void {
  if (!UUID_PATTERN.test(orderId)) {
    throw new CommerceInputError("A valid order id is required");
  }
}

function assertBasketInput(input: BasketSaveInput): void {
  if (
    !Number.isSafeInteger(input.expectedRevision) ||
    input.expectedRevision < 0 ||
    input.expectedRevision > UINT32_MAX
  ) {
    throw new CommerceInputError("A valid expected basket revision is required");
  }
  if (input.selection === null) {
    return;
  }
  if (
    typeof input.selection.offerId !== "string" ||
    input.selection.offerId.length === 0 ||
    input.selection.offerId.length > 64 ||
    !Array.isArray(input.selection.addOnCodes)
  ) {
    throw new CommerceInputError("A valid basket selection is required");
  }
}

function assertCursor(cursor: string | null): void {
  if (cursor === null) {
    return;
  }
  if (cursor.length === 0 || cursor.length > 512) {
    throw new CommerceInputError("A valid cursor is required");
  }
}

async function readBody(response: Response): Promise<unknown> {
  try {
    return await response.json();
  } catch {
    return undefined;
  }
}

export function createCommerceGateway(options: CommerceGatewayOptions): CommerceGateway {
  const baseUrl = normalizeCommerceBaseUrl(options.baseUrl);
  const fetchImpl = options.fetchImpl ?? fetch;

  async function request(accessToken: string, path: string, init: RequestInit): Promise<unknown> {
    let response: Response;
    try {
      const headers = new Headers(init.headers);
      headers.set("Accept", "application/json");
      headers.set("Authorization", `Bearer ${accessToken}`);
      response = await fetchImpl(`${baseUrl}${path}`, {
        ...init,
        headers,
        cache: "no-store",
        credentials: "omit",
        redirect: "error",
      });
    } catch {
      throw new CommerceUnavailableError();
    }

    const body = await readBody(response);
    if (!response.ok) {
      throw new CommerceHttpError(response.status, parseCommerceProblem(body, response.status));
    }
    return body;
  }

  return {
    async getCatalog(accessToken) {
      assertToken(accessToken);
      const body = await request(accessToken, "/v1/commerce/catalog", {
        method: "GET",
      });
      const catalog = readCatalogResponse(body);
      if (catalog === null) {
        throw new CommerceInvalidResponseError();
      }
      return catalog;
    },

    async getBasket(accessToken) {
      assertToken(accessToken);
      const body = await request(accessToken, "/v1/commerce/basket", {
        method: "GET",
      });
      const basket = readBasket(body);
      if (basket === null) {
        throw new CommerceInvalidResponseError();
      }
      return basket;
    },

    async putBasket(accessToken, input) {
      assertToken(accessToken);
      assertBasketInput(input);
      const body = await request(accessToken, "/v1/commerce/basket", {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          selection: input.selection,
          expectedRevision: input.expectedRevision,
        }),
      });
      const basket = readBasket(body);
      if (basket === null) {
        throw new CommerceInvalidResponseError();
      }
      return basket;
    },

    async listOrders(accessToken, cursor) {
      assertToken(accessToken);
      assertCursor(cursor);
      const query = new URLSearchParams({ limit: String(FIXED_PAGE_LIMIT) });
      if (cursor !== null) {
        query.set("cursor", cursor);
      }
      const body = await request(accessToken, `/v1/commerce/orders?${query.toString()}`, {
        method: "GET",
      });
      const page = readOrderPage(body);
      if (page === null) {
        throw new CommerceInvalidResponseError();
      }
      return page;
    },

    async getOrder(accessToken, orderId) {
      assertToken(accessToken);
      assertOrderId(orderId);
      const body = await request(
        accessToken,
        `/v1/commerce/orders/${encodeURIComponent(orderId)}`,
        { method: "GET" },
      );
      const order = readCustomerOrder(body);
      if (order === null) {
        throw new CommerceInvalidResponseError();
      }
      return order;
    },

    async createOrder(accessToken, input) {
      assertToken(accessToken);
      if (
        !UUID_PATTERN.test(input.idempotencyKey) ||
        !Number.isSafeInteger(input.basketRevision) ||
        input.basketRevision < 1 ||
        input.basketRevision > UINT32_MAX
      ) {
        throw new CommerceInputError("A valid checkout intent is required");
      }
      const body = await request(accessToken, "/v1/commerce/orders", {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          "Idempotency-Key": input.idempotencyKey,
        },
        body: JSON.stringify({ basketRevision: input.basketRevision }),
      });
      const created = readCreateOrderResponse(body);
      if (created === null) {
        throw new CommerceInvalidResponseError();
      }
      return created;
    },
  };
}

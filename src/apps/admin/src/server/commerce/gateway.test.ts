import { describe, expect, it } from "bun:test";
import {
  jsonResponse,
  makeAdminOrder,
  makeDeps,
  makeGateway,
  makeOrderPage,
  ORDER_ID,
  OTHER_ORDER_ID,
  requestHeader,
  TENANT_ID,
} from "./fixtures.testing";
import { createAdminCommerce } from "./orders";

const sessionHeaders = new Headers({
  cookie: "teck-admin.session=never-forward-this",
  authorization: "Bearer browser-supplied-value",
});

describe("admin commerce gateway boundary", () => {
  it("uses one authenticated, cookie-free, no-store request to the configured base", async () => {
    const { calls, fetch } = makeGateway([jsonResponse(200, makeOrderPage([makeAdminOrder()]))]);
    const commerce = createAdminCommerce(makeDeps(fetch));

    const result = await commerce.listOrders(sessionHeaders, {
      tenantId: TENANT_ID,
      cursor: null,
    });

    expect(result.kind).toBe("ok");
    expect(calls).toHaveLength(1);
    const call = calls[0];
    if (call === undefined) throw new Error("expected a gateway call");
    expect(call.url).toBe(
      `https://gateway.example/v1/admin/commerce/tenants/${TENANT_ID}/orders?limit=25`,
    );
    expect(call.init.method).toBe("GET");
    expect(call.init.body).toBeUndefined();
    expect(call.init.cache).toBe("no-store");
    expect(call.init.redirect).toBe("error");
    expect(call.init.credentials).toBe("omit");
    expect(requestHeader(call, "authorization")).toBe("Bearer fixture-token");
    expect(requestHeader(call, "cookie")).toBeNull();
    expect(requestHeader(call, "content-type")).toBeNull();
    expect(JSON.stringify(result)).not.toContain("fixture-token");
    expect(JSON.stringify(result)).not.toContain("never-forward-this");
  });

  it("does not call the gateway without a session", async () => {
    const { calls, fetch } = makeGateway([]);
    const commerce = createAdminCommerce(makeDeps(fetch, { getAccessToken: async () => null }));

    const result = await commerce.listOrders(sessionHeaders, {
      tenantId: TENANT_ID,
      cursor: null,
    });

    expect(result.kind).toBe("unauthorized");
    expect(calls).toHaveLength(0);
  });

  it("does not call the gateway when the session scope is unavailable", async () => {
    const { calls, fetch } = makeGateway([]);
    const commerce = createAdminCommerce(makeDeps(fetch, { getScope: async () => null }));

    const result = await commerce.getOrder(sessionHeaders, {
      tenantId: TENANT_ID,
      orderId: ORDER_ID,
    });

    expect(result.kind).toBe("unauthorized");
    expect(calls).toHaveLength(0);
  });

  it("fails closed without a gateway call when auth storage is unavailable", async () => {
    const { calls, fetch } = makeGateway([]);
    const commerce = createAdminCommerce(
      makeDeps(fetch, {
        getAccessToken: async () => {
          throw new Error("auth store offline");
        },
      }),
    );

    const result = await commerce.listOrders(sessionHeaders, {
      tenantId: TENANT_ID,
      cursor: null,
    });

    expect(result.kind).toBe("unavailable");
    expect(calls).toHaveLength(0);
  });

  it("fails closed without a gateway call when the gateway base is not configured", async () => {
    const { calls, fetch } = makeGateway([]);
    const commerce = createAdminCommerce(makeDeps(fetch, { gatewayUrl: "  " }));

    const result = await commerce.listOrders(sessionHeaders, {
      tenantId: TENANT_ID,
      cursor: null,
    });

    expect(result.kind).toBe("unavailable");
    expect(calls).toHaveLength(0);
  });

  it("maps unauthenticated and forbidden gateway responses to session states", async () => {
    const unauthorizedGateway = makeGateway([
      jsonResponse(401, { status: 401, code: "unauthenticated" }),
      jsonResponse(403, { status: 403, code: "forbidden" }),
    ]);
    const commerce = createAdminCommerce(makeDeps(unauthorizedGateway.fetch));

    const unauthorized = await commerce.listOrders(sessionHeaders, {
      tenantId: TENANT_ID,
      cursor: null,
    });
    const forbidden = await commerce.listOrders(sessionHeaders, {
      tenantId: TENANT_ID,
      cursor: null,
    });

    expect(unauthorized.kind).toBe("unauthorized");
    expect(forbidden.kind).toBe("forbidden");
    expect(unauthorizedGateway.calls).toHaveLength(2);
  });

  it("maps a transient dependency failure to unavailable without retrying", async () => {
    const { calls, fetch } = makeGateway([
      jsonResponse(503, { status: 503, code: "dependency_unavailable" }),
    ]);
    const commerce = createAdminCommerce(makeDeps(fetch));

    const result = await commerce.getOrder(sessionHeaders, {
      tenantId: TENANT_ID,
      orderId: ORDER_ID,
    });

    expect(result.kind).toBe("unavailable");
    expect(calls).toHaveLength(1);
  });

  it("rejects malformed order pages instead of rendering a partial result", async () => {
    const { fetch } = makeGateway([jsonResponse(200, { items: [{}], nextCursor: null })]);
    const commerce = createAdminCommerce(makeDeps(fetch));

    const result = await commerce.listOrders(sessionHeaders, {
      tenantId: TENANT_ID,
      cursor: null,
    });

    expect(result.kind).toBe("error");
  });

  it("rejects a detail body for another order without retrying or exposing it", async () => {
    const foreign = makeAdminOrder({ orderId: OTHER_ORDER_ID });
    const { calls, fetch } = makeGateway([jsonResponse(200, foreign)]);
    const commerce = createAdminCommerce(makeDeps(fetch));

    const result = await commerce.getOrder(sessionHeaders, {
      tenantId: TENANT_ID,
      orderId: ORDER_ID,
    });

    expect(result.kind).toBe("error");
    expect(calls).toHaveLength(1);
    expect(JSON.stringify(result)).not.toContain(OTHER_ORDER_ID);
  });

  it("never exposes a staff license or provider field in an admin detail projection", async () => {
    const { calls, fetch } = makeGateway([jsonResponse(200, makeAdminOrder())]);
    const commerce = createAdminCommerce(makeDeps(fetch));

    const result = await commerce.getOrder(sessionHeaders, {
      tenantId: TENANT_ID,
      orderId: ORDER_ID,
    });

    expect(result.kind).toBe("ok");
    expect(calls).toHaveLength(1);
    expect(calls[0]?.url).toBe(
      `https://gateway.example/v1/admin/commerce/tenants/${TENANT_ID}/orders/${ORDER_ID}`,
    );
    expect(JSON.stringify(result)).not.toContain("license");
    expect(JSON.stringify(result)).not.toContain("provider");
  });
});

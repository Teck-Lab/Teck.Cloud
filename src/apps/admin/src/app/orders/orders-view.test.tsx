import { afterEach, describe, expect, it } from "bun:test";
import { cleanup, render, screen } from "@testing-library/react";
import {
  makeAdminOrder,
  makeOrderPage,
  ORDER_ID,
  TENANT_ID,
} from "@/server/commerce/fixtures.testing";
import { type AdminOrder, formatMoney, type OrdersLoadResult } from "@/server/commerce/types";
import { OrdersView } from "./orders-view";

afterEach(cleanup);

function okResult(overrides: {
  items?: AdminOrder[];
  cursor?: string | null;
  nextCursor?: string | null;
}): OrdersLoadResult {
  const items = overrides.items ?? [makeAdminOrder()];
  return {
    kind: "ok",
    tenantId: TENANT_ID,
    cursor: overrides.cursor ?? null,
    items,
    nextCursor: overrides.nextCursor ?? null,
    scope: "opaque-session-scope",
    baseHref: `/orders?tenantId=${TENANT_ID}`,
  };
}

describe("OrdersView", () => {
  it("asks for one tenant and provides no global order view", () => {
    render(<OrdersView result={{ kind: "selectTenant" }} />);

    expect(screen.getByRole("heading", { name: "Orders" })).toBeDefined();
    const form = document.querySelector("form");
    expect(form?.getAttribute("action")).toBe("/orders");
    expect(form?.getAttribute("method")).toBe("get");
    expect(screen.getByLabelText("Tenant UUID")).toBeDefined();
    expect(screen.queryByRole("table")).toBeNull();
  });

  it("renders the authorized rows and an encoded next cursor", () => {
    const items = [
      makeAdminOrder(),
      makeAdminOrder({ orderId: "9c0d1e2f-3a4b-4c5d-8e9f-001122334455" }),
    ];
    render(<OrdersView result={okResult({ items, nextCursor: "opaque+/=" })} />);

    const orderLink = screen
      .getAllByRole("link")
      .find((link) => link.getAttribute("href") === `/orders/${TENANT_ID}/${ORDER_ID}`);
    expect(orderLink).toBeDefined();
    expect(screen.getAllByText(formatMoney(makeAdminOrder().total)).length).toBeGreaterThan(0);

    const nextLinks = screen.getAllByRole("link", { name: /next/i });
    const next = nextLinks[0];
    if (next === undefined) throw new Error("expected a next link");
    const href = next.getAttribute("href") ?? "";
    expect(href).toContain("cursor=opaque%2B%2F%3D");
    expect(href).toContain(`tenantId=${TENANT_ID}`);
    expect(href).not.toContain("cursor=opaque+/=");
  });

  it("omits the next control at the end of the page history", () => {
    render(<OrdersView result={okResult({ cursor: "last-cursor", nextCursor: null })} />);

    expect(screen.queryByRole("link", { name: /next/i })).toBeNull();
  });

  it("shows an empty state without inventing rows", () => {
    render(<OrdersView result={okResult({ items: [], nextCursor: null })} />);

    expect(screen.getByText("No orders are recorded for this tenant yet.")).toBeDefined();
    expect(screen.queryByRole("table")).toBeNull();
  });

  it("shows the tenant form without a stale cursor input", () => {
    render(<OrdersView result={okResult({ cursor: "old", nextCursor: null })} />);

    const form = document.querySelector("form");
    expect(form?.querySelector('input[name="cursor"]')).toBeNull();
    expect(form?.querySelector('input[name="tenantId"]')).not.toBeNull();
  });

  it("shows an error and first-page link for a rejected cursor", () => {
    render(
      <OrdersView
        result={{
          kind: "cursorRejected",
          message: "That page link is no longer valid. Start again from the first page.",
          firstPageHref: `/orders?tenantId=${TENANT_ID}`,
        }}
        tenantInput={TENANT_ID}
      />,
    );

    expect(screen.getByText(/no longer valid/i)).toBeDefined();
    expect(screen.getByRole("link", { name: "First page" }).getAttribute("href")).toBe(
      `/orders?tenantId=${TENANT_ID}`,
    );
  });

  it("shows expired-session and permission states without leaking a token", () => {
    render(<OrdersView result={{ kind: "unauthorized" }} />);
    expect(screen.getByText(/session has expired/i)).toBeDefined();
    expect(screen.getByRole("link", { name: "Sign in" }).getAttribute("href")).toBe("/sign-in");
    cleanup();

    render(<OrdersView result={{ kind: "forbidden" }} />);
    expect(screen.getByText(/do not have permission/i)).toBeDefined();
    cleanup();

    render(<OrdersView result={{ kind: "unavailable" }} />);
    expect(screen.getByText(/temporarily unavailable/i)).toBeDefined();

    expect(document.body.textContent ?? "").not.toContain("fixture-token");
    expect(document.body.textContent ?? "").not.toContain("license");
  });

  it("preserves an attempted tenant value when the UUID is invalid", () => {
    render(
      <OrdersView
        result={{ kind: "invalidTenant", message: "Enter the tenant UUID." }}
        tenantInput="not-a-uuid"
      />,
    );

    const input = screen.getByLabelText("Tenant UUID") as HTMLInputElement;
    expect(input.value).toBe("not-a-uuid");
    expect(screen.getByText("Enter the tenant UUID.")).toBeDefined();
  });

  it("keeps the fixed page contract visible in the table projection", () => {
    const page = makeOrderPage([makeAdminOrder()]);
    expect(page.items).toHaveLength(1);
    render(<OrdersView result={okResult({ items: page.items })} />);

    const text = document.body.textContent ?? "";
    expect(text).not.toContain("license");
    expect(text).not.toContain("checkout");
    expect(text).not.toContain("fixture-token");
  });
});

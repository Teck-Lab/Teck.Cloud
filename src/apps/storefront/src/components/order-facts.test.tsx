import { afterEach, describe, expect, test } from "bun:test";
import { cleanup, render, screen } from "@testing-library/react";
import type { OrderDetailView } from "@/commerce/contracts";
import { OrderFacts } from "./order-facts";

afterEach(cleanup);

const ORDER_ID = "0f0e0d0c-0b0a-4999-8877-665544332211";

const activeOrder: OrderDetailView = {
  id: ORDER_ID,
  subscriptionId: "1a1b1c1d-2e2f-4a3b-8c4d-5e6f708192a3",
  tier: "shared",
  addOnCodes: [],
  total: { amountMinor: 1000, currency: "USD" },
  status: "active",
  revision: 4,
  createdAt: "2026-11-23T19:00:00Z",
  updatedAt: "2026-11-23T20:15:00Z",
  license: {
    keyId: "K1",
    revision: 1,
    expiresAt: "2026-11-23T20:30:00Z",
  },
};

describe("order facts", () => {
  test("offers a license download only for an active order", () => {
    const { container } = render(<OrderFacts order={activeOrder} />);

    const link = screen.getByRole("link", { name: "Download license" });
    expect(link.getAttribute("href")).toBe(`/orders/${ORDER_ID}/license`);
    expect(container.textContent).toContain("K1");
    expect(container.textContent).not.toContain("fixture-license-document");
  });

  test("hides the download for a pending order without a license", () => {
    render(
      <OrderFacts
        order={{
          ...activeOrder,
          status: "pendingPayment",
          license: null,
        }}
      />,
    );

    expect(screen.queryByRole("link", { name: "Download license" })).toBeNull();
    expect(screen.getByText(/appears after this order becomes active/)).toBeTruthy();
  });

  test("renders the nullable checkout notice without inferring payment", () => {
    render(
      <OrderFacts
        order={{ ...activeOrder, status: "pendingPayment", license: null }}
        checkoutNotice="Checkout is not available yet."
      />,
    );

    expect(screen.getByText("Checkout is not available yet.")).toBeTruthy();
  });
});

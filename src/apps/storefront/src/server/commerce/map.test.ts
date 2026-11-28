import { describe, expect, test } from "bun:test";
import { formatMoney, tierLabel } from "@/commerce/contracts";
import type { components } from "./api.generated";
import {
  toBasketView,
  toOffers,
  toOrderDetailView,
  toOrderSummaryView,
  toOrderTableRows,
} from "./map";

type CatalogResponse = components["schemas"]["CatalogResponse"];
type Basket = components["schemas"]["Basket"];
type CustomerOrder = components["schemas"]["CustomerOrder"];

const ORDER_ID = "0f0e0d0c-0b0a-4999-8877-665544332211";
const SUBSCRIPTION_ID = "1a1b1c1d-2e2f-4a3b-8c4d-5e6f708192a3";

const catalog: CatalogResponse = {
  offers: [
    {
      offerId: "postgres-dedicated-monthly",
      tier: "dedicated",
      displayName: "Dedicated PostgreSQL",
      basePrice: { amountMinor: 3000, currency: "USD" },
      addOns: [
        {
          code: "replicaReadAccess",
          displayName: "Read replica access",
          price: { amountMinor: 500, currency: "USD" },
        },
      ],
    },
  ],
};

const activeOrder: CustomerOrder = {
  orderId: ORDER_ID,
  subscriptionId: SUBSCRIPTION_ID,
  tier: "shared",
  addOnCodes: [],
  total: { amountMinor: 1000, currency: "USD" },
  status: "active",
  revision: 4,
  createdAt: "2026-11-23T19:00:00Z",
  updatedAt: "2026-11-23T20:15:00Z",
  license: {
    document: "fixture-license-document-v1",
    keyId: "K1",
    revision: 1,
    expiresAt: "2026-11-23T20:30:00Z",
  },
};

const pendingOrder: CustomerOrder = {
  ...activeOrder,
  status: "pendingPayment",
  license: null,
};

describe("catalog and basket views", () => {
  test("copies only the customer offer fields and server prices", () => {
    const offers = toOffers(catalog);
    expect(offers).toEqual([
      {
        offerId: "postgres-dedicated-monthly",
        tier: "dedicated",
        displayName: "Dedicated PostgreSQL",
        basePrice: { amountMinor: 3000, currency: "USD" },
        addOns: [
          {
            code: "replicaReadAccess",
            displayName: "Read replica access",
            price: { amountMinor: 500, currency: "USD" },
          },
        ],
      },
    ]);
  });

  test("maps a cleared basket without inventing a total", () => {
    const basket: Basket = {
      selection: null,
      revision: 5,
      total: null,
    };
    expect(toBasketView(basket)).toEqual({
      selection: null,
      revision: 5,
      total: null,
    });
  });

  test("does not expose gateway-only basket properties", () => {
    const basket = {
      selection: {
        offerId: "postgres-dedicated-monthly",
        addOnCodes: ["replicaReadAccess"],
      },
      revision: 2,
      total: { amountMinor: 3500, currency: "USD" },
      tenantId: "must-not-leak",
    } as Basket;

    const view = toBasketView(basket);
    expect(Object.keys(view).sort()).toEqual(["revision", "selection", "total"]);
    expect(JSON.stringify(view)).not.toContain("must-not-leak");
  });
});

describe("order views", () => {
  test("keeps only license metadata for an active order", () => {
    const view = toOrderDetailView(activeOrder);

    expect(view.license).toEqual({
      keyId: "K1",
      revision: 1,
      expiresAt: "2026-11-23T20:30:00Z",
    });
    expect(JSON.stringify(view)).not.toContain("fixture-license-document-v1");
    expect(view.subscriptionId).toBe(SUBSCRIPTION_ID);
  });

  test("never exposes a license envelope on a non-active order", () => {
    const view = toOrderDetailView({
      ...activeOrder,
      status: "pendingReadiness",
      license: activeOrder.license,
    });
    expect(view.license).toBeNull();
  });

  test("omits subscription, license and unknown staff fields from list rows", () => {
    const order = {
      ...activeOrder,
      fulfillment: { status: "ready", evidenceReference: "staff-only" },
      actorId: "staff-actor",
    } as CustomerOrder;

    const [row] = toOrderTableRows([order]);
    expect(row?.id).toBe(ORDER_ID);
    expect(row?.href).toBe(`/orders/${ORDER_ID}`);
    expect(row?.tier).toBe("Shared");
    expect(row?.status).toBe("active");
    expect(row?.total).toBe("$10.00");
    expect(Object.keys(row ?? {}).sort()).toEqual(["href", "id", "status", "tier", "total"]);
    expect(JSON.stringify(row)).not.toContain("staff");
    expect(JSON.stringify(row)).not.toContain("fixture-license-document");
  });

  test("preserves an additive unknown status for the shared neutral badge", () => {
    const [row] = toOrderTableRows([{ ...pendingOrder, status: "pendingReview" as never }]);
    expect(row?.status).toBe("pendingReview");
  });

  test("maps a summary without license metadata", () => {
    expect(toOrderSummaryView(activeOrder)).toEqual({
      id: ORDER_ID,
      tier: "shared",
      addOnCodes: [],
      total: { amountMinor: 1000, currency: "USD" },
      status: "active",
      revision: 4,
      createdAt: "2026-11-23T19:00:00Z",
      updatedAt: "2026-11-23T20:15:00Z",
    });
  });
});

describe("presentation helpers", () => {
  test("formats minor-unit money and labels tiers", () => {
    expect(formatMoney({ amountMinor: 3500, currency: "USD" })).toBe("$35.00");
    expect(formatMoney(null)).toBe("Not set");
    expect(tierLabel("shared")).toBe("Shared");
    expect(tierLabel("dedicated")).toBe("Dedicated");
    expect(tierLabel("futureTier")).toBe("Other");
  });
});

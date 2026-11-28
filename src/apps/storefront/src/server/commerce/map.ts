import type {
  AddOnView,
  BasketView,
  MoneyView,
  OfferView,
  OrderDetailView,
  OrderSummaryView,
  OrderTableRow,
} from "@/commerce/contracts";
import { formatMoney, orderHref, tierLabel } from "@/commerce/contracts";
import type { components } from "./api.generated";

type CatalogResponse = components["schemas"]["CatalogResponse"];
type Basket = components["schemas"]["Basket"];
type CustomerOrder = components["schemas"]["CustomerOrder"];

function toMoneyView(money: { amountMinor: number; currency: string }): MoneyView {
  return { amountMinor: money.amountMinor, currency: money.currency };
}

export function toOffers(catalog: CatalogResponse): OfferView[] {
  return catalog.offers.map((offer) => {
    const addOns: AddOnView[] = offer.addOns.map((addOn) => ({
      code: addOn.code,
      displayName: addOn.displayName,
      price: toMoneyView(addOn.price),
    }));
    return {
      offerId: offer.offerId,
      tier: offer.tier,
      displayName: offer.displayName,
      basePrice: toMoneyView(offer.basePrice),
      addOns,
    };
  });
}

export function toBasketView(basket: Basket): BasketView {
  return {
    selection:
      basket.selection === null
        ? null
        : {
            offerId: basket.selection.offerId,
            addOnCodes: [...basket.selection.addOnCodes],
          },
    revision: basket.revision,
    total: basket.total === null ? null : toMoneyView(basket.total),
  };
}

export function toOrderSummaryView(order: CustomerOrder): OrderSummaryView {
  return {
    id: order.orderId,
    tier: order.tier,
    addOnCodes: [...order.addOnCodes],
    total: toMoneyView(order.total),
    status: order.status,
    revision: order.revision,
    createdAt: order.createdAt,
    updatedAt: order.updatedAt,
  };
}

export function toOrderDetailView(order: CustomerOrder): OrderDetailView {
  return {
    ...toOrderSummaryView(order),
    subscriptionId: order.subscriptionId,
    license:
      order.status === "active" && order.license !== null
        ? {
            keyId: order.license.keyId,
            revision: order.license.revision,
            expiresAt: order.license.expiresAt,
          }
        : null,
  };
}

export function toOrderTableRows(orders: readonly CustomerOrder[]): OrderTableRow[] {
  return orders.map((order) => ({
    id: order.orderId,
    href: orderHref(order.orderId),
    tier: tierLabel(order.tier),
    status: order.status,
    total: formatMoney(toMoneyView(order.total)),
  }));
}

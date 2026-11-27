import { redirect } from "next/navigation";
import { FailureNotice } from "@/components/failure-notice";
import { PageFrame, PageHeading } from "@/components/page-frame";
import { TextLink } from "@/components/text-link";
import { putBasketAction } from "@/server/commerce/basket-actions";
import { submitCheckout } from "@/server/commerce/checkout";
import { readCommerceGatewayBaseUrl } from "@/server/commerce/config";
import { createCommerceGateway } from "@/server/commerce/gateway";
import { loadCommerce } from "@/server/commerce/load";
import { toBasketView, toOffers } from "@/server/commerce/map";
import { requireCommerceSession } from "@/server/commerce/session";
import { type BasketCatalog, BasketWorkspace } from "./basket-workspace";

export const dynamic = "force-dynamic";

export default async function BasketPage() {
  const { identity } = await requireCommerceSession("/basket");

  const [catalogResult, basketResult] = await Promise.all([
    loadCommerce(() =>
      createCommerceGateway({
        baseUrl: readCommerceGatewayBaseUrl(),
      }).getCatalog(identity.accessToken),
    ),
    loadCommerce(() =>
      createCommerceGateway({
        baseUrl: readCommerceGatewayBaseUrl(),
      }).getBasket(identity.accessToken),
    ),
  ]);

  if (!basketResult.ok) {
    if (basketResult.kind === "unauthorized") {
      redirect("/sign-in");
    }
    return (
      <PageFrame>
        <PageHeading eyebrow="Commerce" title="Basket" />
        <FailureNotice kind={basketResult.kind} actionHref="/basket" />
      </PageFrame>
    );
  }

  const basket = toBasketView(basketResult.value);
  const catalog: BasketCatalog = catalogResult.ok
    ? { status: "ready", offers: toOffers(catalogResult.value) }
    : {
        status: "failed",
        kind: catalogResult.kind,
        actionHref: catalogResult.kind === "unauthorized" ? "/sign-in" : "/basket",
        actionLabel: catalogResult.kind === "unauthorized" ? "Sign in" : "Try again",
      };

  return (
    <PageFrame>
      <PageHeading
        eyebrow="Commerce"
        title="Basket"
        description="Review the saved selection, then continue to hosted checkout."
        aside={<TextLink href="/catalog">Browse plans</TextLink>}
      />

      <BasketWorkspace
        catalog={catalog}
        basket={basket}
        scope={identity.scope}
        save={putBasketAction}
        submit={submitCheckout}
        refreshHref="/basket"
      />
    </PageFrame>
  );
}

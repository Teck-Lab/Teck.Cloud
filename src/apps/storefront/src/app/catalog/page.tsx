import { redirect } from "next/navigation";
import { BasketEditor } from "@/components/basket-editor";
import { FailureNotice } from "@/components/failure-notice";
import { PageFrame, PageHeading } from "@/components/page-frame";
import { TextLink } from "@/components/text-link";
import { putBasketAction } from "@/server/commerce/basket-actions";
import { readCommerceGatewayBaseUrl } from "@/server/commerce/config";
import { createCommerceGateway } from "@/server/commerce/gateway";
import { loadCommerce } from "@/server/commerce/load";
import { toBasketView, toOffers } from "@/server/commerce/map";
import { requireCommerceSession } from "@/server/commerce/session";

export const dynamic = "force-dynamic";

export default async function CatalogPage() {
  const { identity } = await requireCommerceSession("/catalog");

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

  if (!catalogResult.ok) {
    if (catalogResult.kind === "unauthorized") {
      redirect("/sign-in");
    }
    return (
      <PageFrame>
        <PageHeading
          eyebrow="Commerce"
          title="Database plans"
          description="Choose the plan and add-ons for your organization."
        />
        <FailureNotice kind={catalogResult.kind} actionHref="/catalog" />
      </PageFrame>
    );
  }

  if (!basketResult.ok) {
    if (basketResult.kind === "unauthorized") {
      redirect("/sign-in");
    }
    return (
      <PageFrame>
        <PageHeading
          eyebrow="Commerce"
          title="Database plans"
          description="Choose the plan and add-ons for your organization."
        />
        <FailureNotice kind={basketResult.kind} actionHref="/catalog" />
      </PageFrame>
    );
  }

  return (
    <PageFrame>
      <PageHeading
        eyebrow="Commerce"
        title="Database plans"
        description="Prices, tiers and eligible add-ons are supplied by Teck Commerce for your signed-in organization."
        aside={<TextLink href="/basket">Go to basket</TextLink>}
      />
      <BasketEditor
        catalog={toOffers(catalogResult.value)}
        basket={toBasketView(basketResult.value)}
        save={putBasketAction}
        refreshHref="/catalog"
      />
      <p className="mt-6 text-sm text-muted-foreground">
        Orders and license downloads are available in{" "}
        <TextLink href="/orders">order history</TextLink>.
      </p>
    </PageFrame>
  );
}

import { redirect } from "next/navigation";
import { CheckoutControls } from "@/checkout/controls";
import type { BasketView } from "@/commerce/contracts";
import { BasketEditor } from "@/components/basket-editor";
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

export const dynamic = "force-dynamic";

function CheckoutPanel({ scope, basket }: { scope: string; basket: BasketView }) {
  return (
    <section
      aria-labelledby="checkout-heading"
      className="rounded-lg border border-border bg-card p-4 shadow-sm sm:p-5"
    >
      <h2 id="checkout-heading" className="text-base font-semibold text-card-foreground">
        Checkout
      </h2>
      <p className="mt-2 text-sm text-muted-foreground">
        Checkout opens on the configured payment provider. Payment is only confirmed when the order
        later reports an active or pending state.
      </p>
      {basket.selection === null ? (
        <p className="mt-3 text-sm text-muted-foreground">
          Choose a plan and save it before starting checkout.
        </p>
      ) : null}
      <div className="mt-4">
        <CheckoutControls
          scope={scope}
          basket={{
            revision: basket.revision,
            populated: basket.selection !== null,
          }}
          submit={submitCheckout}
        />
      </div>
    </section>
  );
}

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

  return (
    <PageFrame>
      <PageHeading
        eyebrow="Commerce"
        title="Basket"
        description="Review the saved selection, then continue to hosted checkout."
        aside={<TextLink href="/catalog">Browse plans</TextLink>}
      />

      <div className="grid items-start gap-6 lg:grid-cols-[minmax(0,1fr)_22rem]">
        {catalogResult.ok ? (
          <BasketEditor
            catalog={toOffers(catalogResult.value)}
            basket={basket}
            save={putBasketAction}
            refreshHref="/basket"
          />
        ) : (
          <FailureNotice
            kind={catalogResult.kind}
            actionHref={catalogResult.kind === "unauthorized" ? "/sign-in" : "/basket"}
            actionLabel={catalogResult.kind === "unauthorized" ? "Sign in" : "Try again"}
          />
        )}
        <CheckoutPanel scope={identity.scope} basket={basket} />
      </div>
    </PageFrame>
  );
}

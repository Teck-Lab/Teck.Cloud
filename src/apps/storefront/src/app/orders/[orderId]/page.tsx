import { notFound, redirect } from "next/navigation";
import { CheckoutControls } from "@/checkout/controls";
import { FailureNotice } from "@/components/failure-notice";
import { OrderFacts } from "@/components/order-facts";
import { PageFrame, PageHeading } from "@/components/page-frame";
import { TextLink } from "@/components/text-link";
import { submitCheckout } from "@/server/commerce/checkout";
import { readCommerceGatewayBaseUrl } from "@/server/commerce/config";
import { createCommerceGateway } from "@/server/commerce/gateway";
import { loadCommerce } from "@/server/commerce/load";
import { toOrderDetailView } from "@/server/commerce/map";
import { requireCommerceSession } from "@/server/commerce/session";

export const dynamic = "force-dynamic";

const UUID_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/;

export default async function OrderDetailPage({
  params,
  searchParams,
}: {
  params: Promise<{ orderId: string }>;
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  const { orderId } = await params;
  const returnTo = UUID_PATTERN.test(orderId) ? `/orders/${orderId}` : "/orders";
  const { identity } = await requireCommerceSession(returnTo);
  const query = await searchParams;

  if (!UUID_PATTERN.test(orderId)) {
    notFound();
  }

  const result = await loadCommerce(() =>
    createCommerceGateway({
      baseUrl: readCommerceGatewayBaseUrl(),
    }).getOrder(identity.accessToken, orderId),
  );

  if (!result.ok) {
    if (result.kind === "unauthorized") {
      redirect("/sign-in");
    }
    if (result.kind === "not_found") {
      notFound();
    }
    return (
      <PageFrame>
        <PageHeading
          eyebrow="Commerce"
          title="Order"
          aside={<TextLink href="/orders">Order history</TextLink>}
        />
        <FailureNotice
          kind={result.kind}
          actionHref={
            result.kind === "unavailable" || result.kind === "error"
              ? `/orders/${orderId}`
              : "/basket"
          }
          actionLabel={
            result.kind === "unavailable" || result.kind === "error"
              ? "Try again"
              : "Back to basket"
          }
        />
        <p className="mt-4 text-sm">
          <TextLink href="/orders">View order history</TextLink>
        </p>
      </PageFrame>
    );
  }

  const order = toOrderDetailView(result.value);
  const checkoutNotice = query.checkout === "unavailable" ? "Checkout is not available yet." : null;

  return (
    <PageFrame>
      <PageHeading
        eyebrow="Commerce"
        title="Order"
        description="Status, billing projection and license availability for this order."
        aside={<TextLink href="/orders">Order history</TextLink>}
      />

      <div className="grid items-start gap-6 lg:grid-cols-[minmax(0,1fr)_22rem]">
        <OrderFacts order={order} checkoutNotice={checkoutNotice} />

        <section
          aria-labelledby="order-checkout-heading"
          className="rounded-lg border border-border bg-card p-4 shadow-sm sm:p-5"
        >
          <h2 id="order-checkout-heading" className="text-base font-semibold text-card-foreground">
            Checkout
          </h2>
          <p className="mt-2 text-sm text-muted-foreground">
            Resume only the checkout intent stored in this browser for this order. A payment
            redirect never confirms payment by itself.
          </p>
          <div className="mt-4">
            <CheckoutControls
              scope={identity.scope}
              order={{ id: order.id, status: order.status }}
              submit={submitCheckout}
            />
          </div>
          <p className="mt-4 text-sm">
            <TextLink href="/basket">Back to basket</TextLink>
          </p>
        </section>
      </div>
    </PageFrame>
  );
}

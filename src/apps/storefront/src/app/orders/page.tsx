import { OrderTable } from "@teck/ui/blocks/order-table";
import { Pagination } from "@teck/ui/blocks/pagination";
import { redirect } from "next/navigation";
import { FailureNotice } from "@/components/failure-notice";
import { PageFrame, PageHeading } from "@/components/page-frame";
import { TextLink } from "@/components/text-link";
import { readCommerceGatewayBaseUrl } from "@/server/commerce/config";
import { createCommerceGateway } from "@/server/commerce/gateway";
import { loadCommerce } from "@/server/commerce/load";
import { toOrderTableRows } from "@/server/commerce/map";
import { readCursor } from "@/server/commerce/paging";
import { requireCommerceSession } from "@/server/commerce/session";

export const dynamic = "force-dynamic";

export default async function OrdersPage({
  searchParams,
}: {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  const { identity, paginationScope } = await requireCommerceSession("/orders");
  const query = await searchParams;
  const cursor = readCursor(query.cursor);

  if (cursor.kind === "invalid") {
    return (
      <PageFrame>
        <PageHeading
          eyebrow="Commerce"
          title="Order history"
          description="Recent purchases for your organization."
        />
        <FailureNotice kind="conflict" actionHref="/orders" actionLabel="First page" />
      </PageFrame>
    );
  }

  const result = await loadCommerce(() =>
    createCommerceGateway({
      baseUrl: readCommerceGatewayBaseUrl(),
    }).listOrders(identity.accessToken, cursor.kind === "cursor" ? cursor.value : null),
  );

  if (!result.ok) {
    if (result.kind === "unauthorized") {
      redirect("/sign-in");
    }
    return (
      <PageFrame>
        <PageHeading
          eyebrow="Commerce"
          title="Order history"
          description="Recent purchases for your organization."
        />
        <FailureNotice kind={result.kind} actionHref="/orders" actionLabel="First page" />
      </PageFrame>
    );
  }

  const rows = toOrderTableRows(result.value.items);
  const currentCursor = cursor.kind === "cursor" ? cursor.value : null;

  return (
    <PageFrame>
      <PageHeading
        eyebrow="Commerce"
        title="Order history"
        description="At most 25 orders per page. Page links use opaque cursors supplied by the gateway."
        aside={<TextLink href="/basket">Basket</TextLink>}
      />

      {rows.length === 0 ? (
        <div className="rounded-lg border border-border bg-card p-4 shadow-sm sm:p-5">
          <p className="text-sm text-muted-foreground">
            No orders are available for your organization yet.
          </p>
          <p className="mt-2 text-sm">
            <TextLink href="/catalog">Choose a database plan</TextLink>
          </p>
        </div>
      ) : (
        <OrderTable rows={rows} />
      )}

      <div className="mt-4">
        <Pagination
          baseHref="/orders"
          scope={paginationScope}
          cursor={currentCursor}
          nextCursor={result.value.nextCursor}
        />
      </div>
    </PageFrame>
  );
}

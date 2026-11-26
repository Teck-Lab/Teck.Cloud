import { headers } from "next/headers";
import { type OrderSearchParams, readOrdersQuery } from "@/server/commerce/query";
import { adminCommerce } from "@/server/commerce/runtime";
import { MESSAGES, type OrdersLoadResult } from "@/server/commerce/types";
import { OrdersView } from "./orders-view";

type OrdersPageProps = {
  searchParams: Promise<OrderSearchParams>;
};

function rejectedCursorResult(tenantId: string): OrdersLoadResult {
  return {
    kind: "cursorRejected",
    message: MESSAGES.rejectedCursor,
    firstPageHref: `/orders?tenantId=${encodeURIComponent(tenantId)}`,
  };
}

export default async function OrdersPage({ searchParams }: OrdersPageProps) {
  const query = readOrdersQuery(await searchParams);

  if (query.kind === "noTenant") {
    return <OrdersView result={{ kind: "selectTenant" }} tenantInput="" />;
  }
  if (query.kind === "invalidTenant") {
    return (
      <OrdersView
        result={{ kind: "invalidTenant", message: MESSAGES.invalidTenant }}
        tenantInput={query.attemptedTenantId}
      />
    );
  }
  if (query.kind === "rejectedCursor") {
    return (
      <OrdersView result={rejectedCursorResult(query.tenantId)} tenantInput={query.tenantId} />
    );
  }

  const result = await adminCommerce().listOrders(await headers(), {
    tenantId: query.tenantId,
    cursor: query.cursor,
  });
  return <OrdersView result={result} tenantInput={query.tenantId} />;
}

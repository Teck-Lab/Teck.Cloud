import { headers } from "next/headers";
import { adminCommerce } from "@/server/commerce/runtime";
import { MESSAGES } from "@/server/commerce/types";
import { isUuid } from "@/server/commerce/validate";
import { decideOrderAction, recordPlacementEvidenceAction } from "./actions";
import { OrderDetailView } from "./order-detail-view";

type OrderDetailPageProps = {
  params: Promise<{ tenantId: string; orderId: string }>;
};

const actions = {
  decide: decideOrderAction,
  recordEvidence: recordPlacementEvidenceAction,
};

export default async function OrderDetailPage({ params }: OrderDetailPageProps) {
  const { tenantId, orderId } = await params;

  if (!isUuid(tenantId) || !isUuid(orderId)) {
    return (
      <OrderDetailView
        result={{ kind: "invalid", message: MESSAGES.invalidOrder }}
        actions={actions}
      />
    );
  }

  const result = await adminCommerce().getOrder(await headers(), {
    tenantId,
    orderId,
  });
  return <OrderDetailView result={result} actions={actions} />;
}

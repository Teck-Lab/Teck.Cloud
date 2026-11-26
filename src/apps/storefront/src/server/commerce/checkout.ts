"use server";

import { headers } from "next/headers";
import type { CheckoutResult } from "@/checkout/types";
import { getCommerceIdentity } from "@/server/auth";
import { type CheckoutSubmission, performCheckout, readCheckoutOrigin } from "./checkout-core";
import { readCommerceGatewayBaseUrl } from "./config";
import { createCommerceGateway } from "./gateway";

/**
 * Same-origin authenticated checkout action. It re-derives the opaque session
 * scope on every invocation and compares it with the browser-supplied value
 * before any gateway call. The gateway token and identity never leave Next.
 */
export async function submitCheckout(input: CheckoutSubmission): Promise<CheckoutResult> {
  const requestHeaders = await headers();

  let identity: { accessToken: string; scope: string } | null;
  try {
    identity = await getCommerceIdentity(requestHeaders);
  } catch {
    return { status: 503, code: "dependency_unavailable" };
  }

  return performCheckout(
    {
      identity,
      checkoutOrigin: readCheckoutOrigin(),
      createOrder: async (orderInput) => {
        const gateway = createCommerceGateway({
          baseUrl: readCommerceGatewayBaseUrl(),
        });
        return gateway.createOrder(orderInput.accessToken, {
          basketRevision: orderInput.basketRevision,
          idempotencyKey: orderInput.idempotencyKey,
        });
      },
    },
    input,
  );
}

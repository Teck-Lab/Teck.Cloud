"use server";

import { headers } from "next/headers";
import type { BasketActionResult } from "@/commerce/contracts";
import { getCommerceIdentity } from "@/server/auth";
import { performBasketSave, validateBasketSaveInput } from "./basket";
import { readCommerceGatewayBaseUrl } from "./config";
import { createCommerceGateway } from "./gateway";

/**
 * Same-origin server action for basket create/update/clear. The browser only
 * supplies the selection and last observed revision; authorization, pricing
 * and canonicalisation stay with the gateway.
 */
export async function putBasketAction(input: unknown): Promise<BasketActionResult> {
  const parsed = validateBasketSaveInput(input);
  if (parsed === null) {
    return { status: 400, code: "invalid_request" };
  }

  const requestHeaders = await headers();

  let identity: { accessToken: string; scope: string } | null;
  try {
    identity = await getCommerceIdentity(requestHeaders);
  } catch {
    return { status: 503, code: "dependency_unavailable" };
  }

  if (identity === null) {
    return { status: 401, code: "unauthenticated" };
  }

  const accessToken = identity.accessToken;

  return performBasketSave(
    {
      putBasket: (basketInput) => {
        const gateway = createCommerceGateway({
          baseUrl: readCommerceGatewayBaseUrl(),
        });
        return gateway.putBasket(accessToken, basketInput);
      },
    },
    parsed,
  );
}

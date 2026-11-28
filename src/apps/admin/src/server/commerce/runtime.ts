import { appSessionScope, getGatewayAccessToken } from "@/server/auth";
import { createAdminCommerce } from "./orders";
import type { CommerceDeps } from "./types";

/**
 * Runtime wiring only. The auth and gateway modules stay behind the server
 * boundary; pages and server actions never see the bearer token.
 */
export function adminCommerce() {
  const deps: CommerceDeps = {
    gatewayUrl: process.env.COMMERCE_GATEWAY_URL ?? "",
    fetch: (input, init) => globalThis.fetch(input, init),
    getAccessToken: getGatewayAccessToken,
    getScope: appSessionScope,
  };
  return createAdminCommerce(deps);
}

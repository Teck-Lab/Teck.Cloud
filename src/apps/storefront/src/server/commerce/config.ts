import "server-only";

import { CommerceInputError } from "./problems";

/**
 * Reads the server-only gateway base URL lazily so Next builds never require a
 * live gateway or configuration. The value never reaches browser code.
 */
export function readCommerceGatewayBaseUrl(
  env: Record<string, string | undefined> = process.env,
): string {
  const raw = env.COMMERCE_GATEWAY_URL?.trim() ?? "";
  if (raw.length === 0) {
    throw new CommerceInputError("COMMERCE_GATEWAY_URL is not configured");
  }
  return raw;
}

import "server-only";

import type { components } from "./api.generated";

type CustomerOrder = components["schemas"]["CustomerOrder"];

export type LicenseDownload = {
  filename: string;
  body: string;
  contentType: string;
};

/**
 * Produces a download only for the caller's own currently active order with a
 * non-null signed license envelope. The document is never placed in page props.
 */
export function toLicenseDownload(order: CustomerOrder): LicenseDownload | null {
  if (order.status !== "active" || order.license === null) {
    return null;
  }

  const safeKey =
    order.license.keyId
      .replace(/[^A-Za-z0-9_-]+/g, "-")
      .replace(/^-+|-+$/g, "")
      .slice(0, 64) || "license";

  return {
    filename: `teck-license-${safeKey}.txt`,
    body: order.license.document,
    contentType: "text/plain; charset=utf-8",
  };
}

import { describe, expect, test } from "bun:test";
import type { components } from "./api.generated";
import { toLicenseDownload } from "./license";

type CustomerOrder = components["schemas"]["CustomerOrder"];

const ORDER_ID = "0f0e0d0c-0b0a-4999-8877-665544332211";
const SUBSCRIPTION_ID = "1a1b1c1d-2e2f-4a3b-8c4d-5e6f708192a3";

const activeOrder: CustomerOrder = {
  orderId: ORDER_ID,
  subscriptionId: SUBSCRIPTION_ID,
  tier: "shared",
  addOnCodes: [],
  total: { amountMinor: 1000, currency: "USD" },
  status: "active",
  revision: 4,
  createdAt: "2026-11-23T19:00:00Z",
  updatedAt: "2026-11-23T20:15:00Z",
  license: {
    document: "fixture-license-document-v1",
    keyId: "K1/../unsafe",
    revision: 1,
    expiresAt: "2026-11-23T20:30:00Z",
  },
};

describe("license download", () => {
  test("returns the signed document only for an active envelope", () => {
    expect(toLicenseDownload(activeOrder)).toEqual({
      filename: "teck-license-K1-unsafe.txt",
      body: "fixture-license-document-v1",
      contentType: "text/plain; charset=utf-8",
    });
  });

  test("returns no download for non-active orders or a null license", () => {
    expect(toLicenseDownload({ ...activeOrder, status: "pendingReadiness" })).toBeNull();
    expect(toLicenseDownload({ ...activeOrder, license: null })).toBeNull();
  });

  test("sanitises a hostile key id into a safe attachment name", () => {
    const download = toLicenseDownload({
      ...activeOrder,
      license: {
        document: "doc",
        keyId: "../../\r\nSet-Cookie: x",
        revision: 1,
        expiresAt: "2026-11-23T20:30:00Z",
      },
    });
    expect(download?.filename).toBe("teck-license-Set-Cookie-x.txt");
    expect(download?.filename).not.toContain("/");
    expect(download?.filename).not.toContain("\r");
  });
});

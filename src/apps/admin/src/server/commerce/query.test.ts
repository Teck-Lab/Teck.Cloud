import { describe, expect, it } from "bun:test";
import { TENANT_ID } from "./fixtures.testing";
import { readOrdersQuery } from "./query";

describe("admin orders URL query", () => {
  it("requires a tenant selection before any read", () => {
    expect(readOrdersQuery({ cursor: "stale-cursor" })).toEqual({ kind: "noTenant" });
    expect(readOrdersQuery({ tenantId: "" })).toEqual({ kind: "noTenant" });
  });

  it("accepts exactly one canonical lowercase tenant UUID", () => {
    expect(readOrdersQuery({ tenantId: TENANT_ID })).toEqual({
      kind: "ok",
      tenantId: TENANT_ID,
      cursor: null,
    });
    expect(readOrdersQuery({ tenantId: TENANT_ID.toUpperCase() }).kind).toBe("invalidTenant");
    expect(readOrdersQuery({ tenantId: [TENANT_ID] }).kind).toBe("invalidTenant");
  });

  it("keeps only the current opaque cursor and rejects malformed values", () => {
    expect(readOrdersQuery({ tenantId: TENANT_ID, cursor: "opaque+/=" })).toEqual({
      kind: "ok",
      tenantId: TENANT_ID,
      cursor: "opaque+/=",
    });
    expect(readOrdersQuery({ tenantId: TENANT_ID, cursor: "x".repeat(513) }).kind).toBe(
      "rejectedCursor",
    );
    expect(readOrdersQuery({ tenantId: TENANT_ID, cursor: [""] }).kind).toBe("rejectedCursor");
  });

  it("drops a stale cursor when the tenant changes", () => {
    expect(readOrdersQuery({ tenantId: TENANT_ID, cursor: "" })).toEqual({
      kind: "ok",
      tenantId: TENANT_ID,
      cursor: null,
    });
  });
});

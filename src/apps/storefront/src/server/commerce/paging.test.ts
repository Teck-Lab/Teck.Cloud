import { describe, expect, test } from "bun:test";
import { readCursor } from "./paging";

describe("orders cursor", () => {
  test("reads no cursor only when the query key is absent", () => {
    expect(readCursor(undefined)).toEqual({ kind: "none" });
  });

  test("accepts one opaque cursor value from the current URL", () => {
    expect(readCursor("cursor_orders_page_2")).toEqual({
      kind: "cursor",
      value: "cursor_orders_page_2",
    });
  });

  test("rejects empty, repeated and over-long cursor values", () => {
    expect(readCursor("")).toEqual({ kind: "invalid" });
    expect(readCursor(["a", "b"])).toEqual({ kind: "invalid" });
    expect(readCursor("x".repeat(513))).toEqual({ kind: "invalid" });
  });
});

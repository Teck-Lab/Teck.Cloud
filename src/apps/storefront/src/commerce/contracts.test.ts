import { describe, expect, test } from "bun:test";
import { formatMoney } from "./contracts";

describe("formatMoney", () => {
  test("formats a two-decimal currency from minor units", () => {
    expect(formatMoney({ amountMinor: 3500, currency: "USD" })).toBe("$35.00");
  });

  test("formats a zero-decimal currency without inflating the amount", () => {
    expect(formatMoney({ amountMinor: 3500, currency: "JPY" })).toBe("¥3,500");
  });

  test("formats a three-decimal currency without dropping the third digit", () => {
    expect(formatMoney({ amountMinor: 1234, currency: "KWD" })).toBe("KWD\u00a01.234");
    expect(formatMoney({ amountMinor: 5, currency: "KWD" })).toBe("KWD\u00a00.005");
  });

  test("interprets the minor-unit ratio exactly at the contract bound", () => {
    const bound = 9_007_199_254_740_991;
    expect(formatMoney({ amountMinor: bound, currency: "JPY" })).toBe("¥9,007,199,254,740,991");
    expect(formatMoney({ amountMinor: bound, currency: "KWD" })).toBe(
      "KWD\u00a09,007,199,254,740.991",
    );
    expect(formatMoney({ amountMinor: bound, currency: "USD" })).toBe("$90,071,992,547,409.91");
  });

  test("returns the fallback for an unreadable currency", () => {
    expect(formatMoney({ amountMinor: 1200, currency: "US" })).toBe("1200 US");
  });

  test("reports a missing amount as not set", () => {
    expect(formatMoney(null)).toBe("Not set");
  });
});

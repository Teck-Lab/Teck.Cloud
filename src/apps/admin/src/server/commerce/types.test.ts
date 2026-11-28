import { describe, expect, test } from "bun:test";
import { formatMoney } from "./types";

function total(amountMinor: number, currency: string) {
  return { amountMinor, currency };
}

describe("formatMoney", () => {
  test("formats a two-decimal currency from minor units", () => {
    expect(formatMoney(total(3500, "USD"))).toBe("$35.00");
  });

  test("formats a zero-decimal currency without inflating the amount", () => {
    expect(formatMoney(total(3500, "JPY"))).toBe("¥3,500");
  });

  test("formats a three-decimal currency without dropping the third digit", () => {
    expect(formatMoney(total(1234, "KWD"))).toBe("KWD\u00a01.234");
    expect(formatMoney(total(5, "KWD"))).toBe("KWD\u00a00.005");
  });

  test("interprets the minor-unit ratio exactly at the contract bound", () => {
    const bound = 9_007_199_254_740_991;
    expect(formatMoney(total(bound, "JPY"))).toBe("¥9,007,199,254,740,991");
    expect(formatMoney(total(bound, "KWD"))).toBe("KWD\u00a09,007,199,254,740.991");
    expect(formatMoney(total(bound, "USD"))).toBe("$90,071,992,547,409.91");
  });

  test("keeps the existing fallback for an unreadable currency", () => {
    expect(formatMoney(total(1200, "US"))).toBe("12.00 US");
  });
});

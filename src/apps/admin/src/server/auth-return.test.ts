import { describe, expect, test } from "bun:test";

import { safeReturnTo } from "./auth-claims";
import { performSignIn } from "./auth-sign-in";

const BASE = "https://admin.example.test";
const ORIGIN = new URL(BASE).origin;

function expectSameAppOrigin(reference: string): void {
  expect(reference.startsWith("/")).toBe(true);
  expect(reference.startsWith("//")).toBe(false);
  expect(reference.includes("\\")).toBe(false);
  // Resolve exactly as the browser does when following the callback redirect:
  // the assertion is on this final destination, not the intermediate parse.
  expect(new URL(reference, BASE).origin).toBe(ORIGIN);
}

describe("safeReturnTo final destination", () => {
  test("keeps internal paths, queries and fragments on this origin", () => {
    const cases: readonly [string, string][] = [
      ["/", "/"],
      ["/tenants", "/tenants"],
      ["/tenants?cursor=abc", "/tenants?cursor=abc"],
      ["/tenants?cursor=abc#orders", "/tenants?cursor=abc#orders"],
      ["/tenants/acme/orders?status=pending#top", "/tenants/acme/orders?status=pending#top"],
      ["/tenants/../orders", "/orders"],
      ["/a/..", "/"],
      ["/a//b", "/a//b"],
    ];
    for (const [value, expected] of cases) {
      const returned = safeReturnTo(value, BASE);
      expect(returned).toBe(expected);
      expectSameAppOrigin(returned);
    }
  });

  test("ambiguous normalization fails closed to the default", () => {
    const cases: readonly unknown[] = [
      "/.//attacker.example",
      "/.%2e//attacker.example",
      "/%2e.//attacker.example",
      "/%2e//attacker.example",
      "/%2E//attacker.example",
      "/..//attacker.example",
      "/%2e%2e//attacker.example",
      "/%2E%2E//attacker.example",
      "/a/..//attacker.example",
      "/a/.%2e//attacker.example",
      "/././/attacker.example",
      "/%2e%2e/%2e//attacker.example",
      "//attacker.example/tenants",
      "/\\attacker.example",
      "https://attacker.example/tenants",
      "javascript:alert(1)",
      "/line\nbreak",
      "/nul\u0000byte",
      "",
      "relative/path",
      null,
      undefined,
      42,
    ];
    for (const value of cases) {
      const returned = safeReturnTo(value, BASE);
      expect(returned).toBe("/");
      expectSameAppOrigin(returned);
    }
    expect(safeReturnTo("/tenants", "not an absolute URL")).toBe("/");
  });

  test("encoded separators stay inside the app origin", () => {
    // The browser does not decode these into an authority, yet the returned
    // reference must still resolve on this app's origin.
    const cases = ["/%2f%2fattacker.example", "/%5cattacker.example", "/%20/attacker.example"];
    for (const value of cases) {
      expectSameAppOrigin(safeReturnTo(value, BASE));
    }
  });

  test("sign-in forwards only the sanitized relative callback", async () => {
    let callbackURL: string | undefined;
    const target = await performSignIn({
      api: {
        async signInSocial({ body }) {
          callbackURL = body.callbackURL;
          return { url: "https://identity.example.test/authorize" };
        },
      },
      app: "admin",
      baseURL: BASE,
      headers: new Headers({ origin: ORIGIN }),
      returnTo: "/.//attacker.example",
      organization: null,
    });
    expect(callbackURL).toBe("/");
    expect(target).toBe("https://identity.example.test/authorize");
  });
});

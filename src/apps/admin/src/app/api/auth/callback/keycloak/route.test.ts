import { afterAll, beforeAll, describe, expect, test } from "bun:test";
import { readdirSync, statSync } from "node:fs";
import { join } from "node:path";

import { GET } from "./route";

const ENV_KEYS = [
  "BETTER_AUTH_URL",
  "BETTER_AUTH_SECRET",
  "AUTH_DATABASE_URL",
  "KEYCLOAK_ISSUER",
  "KEYCLOAK_CLIENT_ID",
  "KEYCLOAK_CLIENT_SECRET",
  "COMMERCE_GATEWAY_URL",
] as const;

const saved = new Map<string, string | undefined>();

beforeAll(() => {
  for (const key of ENV_KEYS) {
    saved.set(key, process.env[key]);
    delete process.env[key];
  }
});

afterAll(() => {
  for (const key of ENV_KEYS) {
    const value = saved.get(key);
    if (value === undefined) delete process.env[key];
    else process.env[key] = value;
  }
});

function callbackRequest(path: string): Request {
  return new Request(`https://admin.example.test${path}`);
}

function routeFiles(directory: string): string[] {
  return readdirSync(directory).flatMap((name) => {
    const path = join(directory, name);
    if (statSync(path).isDirectory()) return routeFiles(path);
    return name === "route.ts" ? [path] : [];
  });
}

describe("auth HTTP surface", () => {
  test("only the exact Keycloak callback path is routed", async () => {
    for (const path of [
      "/api/auth/get-access-token",
      "/api/auth/refresh-token",
      "/api/auth/sign-in/social",
      "/api/auth/callback/keycloak-other",
      "/api/auth",
    ]) {
      const response = await GET(callbackRequest(path));
      expect(response.status).toBe(404);
      expect(response.headers.get("cache-control")).toBe("no-store");
    }
  });

  test("missing runtime configuration is unavailable, not a session", async () => {
    const response = await GET(
      callbackRequest("/api/auth/callback/keycloak?code=code&state=state"),
    );
    expect(response.status).toBe(503);
    expect(await response.text()).toBe("Authentication is temporarily unavailable.");
  });

  test("no generic Better Auth catch-all route exists", () => {
    const authRoot = join(import.meta.dir, "../..");
    const files = routeFiles(authRoot);
    expect(files.map((file) => file.replace(`${authRoot}/`, ""))).toEqual([
      "callback/keycloak/route.ts",
    ]);
  });
});

import { describe, expect, test } from "bun:test";
import { createHmac } from "node:crypto";
import { APIError } from "better-auth/api";
import type { Pool } from "pg";

import { type AuthApi, AuthUnavailableError, createAuthAccess } from "./auth";
import {
  assertSameOrigin,
  CrossOriginActionError,
  readJwtClaims,
  safeReturnTo,
} from "./auth-claims";
import { buildAuthOptions } from "./auth-config";
import { AuthConfigError, type AuthEnv, readAuthEnv } from "./auth-env";
import { buildSignInScopes, performSignIn, SignInInputError } from "./auth-sign-in";

const SECRET = "admin-auth-test-secret-at-least-32-characters";
const ISSUER = "https://identity.example.test/realms/teck";

const ENV: AuthEnv = {
  app: "admin",
  baseURL: "https://admin.example.test",
  secret: SECRET,
  databaseURL: "postgres://auth:auth@db.example.test/admin_auth",
  issuer: ISSUER,
  clientId: "admin-web",
  clientSecret: "client-secret",
  gatewayURL: "https://gateway.example.test",
};

function jwt(claims: Record<string, unknown>): string {
  const encode = (value: unknown) => Buffer.from(JSON.stringify(value)).toString("base64url");
  return `${encode({ alg: "none" })}.${encode(claims)}.signature`;
}

function accessToken(overrides: Record<string, unknown> = {}): string {
  return jwt({ iss: ISSUER, sub: "subject-1", ...overrides });
}

interface FakeOptions {
  readonly session?: unknown;
  readonly accounts?: readonly unknown[];
  readonly token?: string;
  readonly sessionError?: Error;
  readonly tokenError?: Error;
}

function fakeApi(
  calls: { accounts: number; token: unknown[] },
  options: FakeOptions = {},
): AuthApi {
  return {
    async getSession() {
      if (options.sessionError) throw options.sessionError;
      if (options.session === undefined) return null;
      return options.session as never;
    },
    async listUserAccounts() {
      calls.accounts += 1;
      return (options.accounts ?? []) as never;
    },
    async getAccessToken(input) {
      calls.token.push(input.body);
      if (options.tokenError) throw options.tokenError;
      return { accessToken: options.token ?? accessToken() };
    },
  };
}

const session = {
  session: { id: "session-1", userId: "user-1" },
  user: { id: "user-1" },
};

function account(overrides: Record<string, unknown> = {}) {
  return {
    id: "account-row-1",
    providerId: "keycloak",
    accountId: "subject-1",
    userId: "user-1",
    accessToken: "encrypted",
    ...overrides,
  };
}

describe("admin gateway access", () => {
  test("no session returns null without account or token calls", async () => {
    const calls = { accounts: 0, token: [] as unknown[] };
    const access = createAuthAccess({ api: fakeApi(calls, { session: null }), env: ENV });
    await expect(access.getGatewayAccessToken(new Headers())).resolves.toBeNull();
    expect(calls).toEqual({ accounts: 0, token: [] });
  });

  test("ambiguous or foreign accounts fail closed without a token", async () => {
    for (const accounts of [
      [],
      [account(), account({ id: "account-row-2", accountId: "subject-2" })],
      [account({ providerId: "credential" })],
      [account({ userId: "another-user" })],
    ]) {
      const calls = { accounts: 0, token: [] as unknown[] };
      const access = createAuthAccess({ api: fakeApi(calls, { session, accounts }), env: ENV });
      await expect(access.getGatewayAccessToken(new Headers())).resolves.toBeNull();
      expect(calls.token).toEqual([]);
    }
  });

  test("uses the server-side auth row id and narrows issuer and subject", async () => {
    const calls = { accounts: 0, token: [] as unknown[] };
    const access = createAuthAccess({
      api: fakeApi(calls, { session, accounts: [account()] }),
      env: ENV,
    });
    await expect(access.getGatewayAccessToken(new Headers())).resolves.toBe(accessToken());
    expect(calls.token).toEqual([{ accountId: "account-row-1" }]);

    for (const token of [
      accessToken({ iss: "https://other.example.test/realms/teck" }),
      accessToken({ sub: "other-subject" }),
    ]) {
      const mismatched = createAuthAccess({
        api: fakeApi(calls, { session, accounts: [account()], token }),
        env: ENV,
      });
      await expect(mismatched.getGatewayAccessToken(new Headers())).resolves.toBeNull();
    }
  });

  test("API errors require sign-in; store failures are unavailable", async () => {
    const apiError = createAuthAccess({
      api: fakeApi(
        { accounts: 0, token: [] },
        {
          session,
          accounts: [account()],
          tokenError: APIError.from("BAD_REQUEST", {
            code: "FAILED_TO_GET_ACCESS_TOKEN",
            message: "expired",
          }),
        },
      ),
      env: ENV,
    });
    await expect(apiError.getGatewayAccessToken(new Headers())).resolves.toBeNull();

    const unavailable = createAuthAccess({
      api: fakeApi({ accounts: 0, token: [] }, { sessionError: new Error("pool failed") }),
      env: ENV,
    });
    await expect(unavailable.getGatewayAccessToken(new Headers())).rejects.toBeInstanceOf(
      AuthUnavailableError,
    );
  });

  test("session scope is opaque and does not depend on a tenant", async () => {
    const access = createAuthAccess({
      api: fakeApi({ accounts: 0, token: [] }, { session }),
      env: ENV,
    });
    const scope = await access.appSessionScope(new Headers());
    const expected = createHmac("sha256", SECRET)
      .update(
        JSON.stringify(["pagination-session-scope:v1", "admin", ISSUER, "user-1", "session-1", ""]),
      )
      .digest("base64url");
    expect(scope).toBe(expected);
    expect(scope as string).not.toContain("user-1");
  });
});

describe("admin sign-in and narrowing", () => {
  const base = {
    app: "admin" as const,
    baseURL: ENV.baseURL,
    headers: new Headers({ origin: ENV.baseURL }),
    returnTo: "/tenants",
  };

  test("admin requests base scopes and ignores any organization input", () => {
    expect(buildSignInScopes("admin", "ignored")).toEqual(["openid", "profile", "email"]);
    expect(() => buildSignInScopes("storefront", "")).toThrow(SignInInputError);
  });

  test("starts Keycloak without a tenant parameter", async () => {
    const bodies: unknown[] = [];
    await performSignIn({
      ...base,
      organization: "ignored",
      api: {
        async signInSocial(input) {
          bodies.push(input.body);
          return { url: "https://identity.example.test/authorize" };
        },
      },
    });
    expect(bodies).toEqual([
      {
        provider: "keycloak",
        callbackURL: "/tenants",
        errorCallbackURL: "/sign-in?error=sign-in",
        scopes: ["openid", "profile", "email"],
      },
    ]);
  });

  test("foreign origins send no provider request", async () => {
    let called = false;
    await expect(
      performSignIn({
        ...base,
        headers: new Headers({ origin: "https://evil.example" }),
        organization: null,
        api: {
          async signInSocial() {
            called = true;
            return { url: "https://identity.example.test/authorize" };
          },
        },
      }),
    ).rejects.toBeInstanceOf(CrossOriginActionError);
    expect(called).toBe(false);
  });

  test("return URLs stay local and JWT decoding rejects malformed payloads", () => {
    expect(safeReturnTo("/tenants?cursor=abc", ENV.baseURL)).toBe("/tenants?cursor=abc");
    expect(safeReturnTo("//evil.example", ENV.baseURL)).toBe("/");
    expect(readJwtClaims(accessToken())?.sub).toBe("subject-1");
    expect(readJwtClaims("a.not-json.c")).toBeNull();
    expect(() => assertSameOrigin(new Headers(), ENV.baseURL)).toThrow(CrossOriginActionError);
  });
});

describe("admin configuration", () => {
  test("missing env fails closed", () => {
    expect(() => readAuthEnv({}, "admin")).toThrow(AuthConfigError);
  });

  test("separate cookie and auth boundaries are explicit", async () => {
    const options = buildAuthOptions(ENV, "admin");
    expect(options.advanced?.cookiePrefix).toBe("teck-admin");
    expect(options.account?.accountLinking).toEqual({
      enabled: false,
      disableImplicitLinking: true,
    });
    expect(options.session?.cookieCache).toEqual({ enabled: false });
    expect(options.session?.additionalFields).toBeUndefined();
    expect(options.emailAndPassword?.enabled).toBe(false);
    expect(options.account?.storeAccountCookie).toBe(false);
    expect(options.account?.encryptOAuthTokens).toBe(true);
    expect(options.advanced?.defaultCookieAttributes).toMatchObject({
      httpOnly: true,
      sameSite: "lax",
      secure: true,
    });

    const plugins = options.plugins as unknown as { id?: string }[];
    expect(plugins.map((plugin) => plugin.id)).toEqual(["generic-oauth", "next-cookies"]);

    const pool = options.database as Pool;
    await pool.end();
  });
});

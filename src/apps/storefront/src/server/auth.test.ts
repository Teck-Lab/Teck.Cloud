import { describe, expect, test } from "bun:test";
import { createHmac } from "node:crypto";
import { APIError } from "better-auth/api";
import type { Pool } from "pg";
import { type AuthApi, AuthUnavailableError, createAuthAccess } from "./auth";
import {
  assertSameOrigin,
  CrossOriginActionError,
  canonicalOrganizationId,
  deriveSessionOrganization,
  parseOrganizationAlias,
  readJwtClaims,
  safeReturnTo,
  singleOrganizationId,
} from "./auth-claims";
import { buildAuthOptions, STOREFRONT_SESSION_FIELDS } from "./auth-config";
import { AuthConfigError, type AuthEnv, readAuthEnv } from "./auth-env";
import { buildSignInScopes, performSignIn, SignInInputError } from "./auth-sign-in";

const SECRET = "storefront-auth-test-secret-at-least-32-characters";
const ISSUER = "https://identity.example.test/realms/teck";
const ORG = "aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee";
const OTHER_ORG = "11111111-2222-4333-8444-555555555555";

const ENV: AuthEnv = {
  app: "storefront",
  baseURL: "https://storefront.example.test",
  secret: SECRET,
  databaseURL: "postgres://auth:auth@db.example.test/storefront_auth",
  issuer: ISSUER,
  clientId: "storefront-web",
  clientSecret: "client-secret",
  gatewayURL: "https://gateway.example.test",
};

function jwt(claims: Record<string, unknown>): string {
  const encode = (value: unknown) => Buffer.from(JSON.stringify(value)).toString("base64url");
  return `${encode({ alg: "none" })}.${encode(claims)}.signature`;
}

function accessToken(overrides: Record<string, unknown> = {}): string {
  return jwt({
    iss: ISSUER,
    sub: "subject-1",
    organization: { alpha: { id: ORG } },
    ...overrides,
  });
}

interface FakeOptions {
  readonly session?: unknown;
  readonly accounts?: readonly unknown[];
  readonly token?: string;
  readonly sessionError?: Error;
  readonly accountError?: Error;
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
      if (options.accountError) throw options.accountError;
      return (options.accounts ?? []) as never;
    },
    async getAccessToken(input) {
      calls.token.push(input.body);
      if (options.tokenError) throw options.tokenError;
      return { accessToken: options.token ?? accessToken() };
    },
  };
}

function boundSession(organizationId: unknown = ORG) {
  return {
    session: { id: "session-1", userId: "user-1", commerceOrganizationId: organizationId },
    user: { id: "user-1" },
  };
}

function linkedAccount(overrides: Record<string, unknown> = {}) {
  return {
    id: "account-row-1",
    providerId: "keycloak",
    accountId: "subject-1",
    userId: "user-1",
    accessToken: "encrypted-token",
    ...overrides,
  };
}

describe("gateway access", () => {
  test("no session returns null without touching accounts or tokens", async () => {
    const calls = { accounts: 0, token: [] as unknown[] };
    const access = createAuthAccess({
      api: fakeApi(calls, { session: null }),
      env: ENV,
      app: "storefront",
    });

    await expect(access.getGatewayAccessToken(new Headers())).resolves.toBeNull();
    expect(calls.accounts).toBe(0);
    expect(calls.token).toEqual([]);
  });

  test("ambiguous Keycloak accounts return null and do not fetch a token", async () => {
    const calls = { accounts: 0, token: [] as unknown[] };
    const access = createAuthAccess({
      api: fakeApi(calls, {
        session: boundSession(),
        accounts: [linkedAccount(), linkedAccount({ id: "account-row-2", accountId: "subject-2" })],
      }),
      env: ENV,
      app: "storefront",
    });

    await expect(access.getGatewayAccessToken(new Headers())).resolves.toBeNull();
    expect(calls.accounts).toBe(1);
    expect(calls.token).toEqual([]);
  });

  test("missing or unlinked accounts fail closed", async () => {
    for (const accounts of [
      [],
      [linkedAccount({ providerId: "credential" })],
      [linkedAccount({ userId: "another-user" })],
    ]) {
      const calls = { accounts: 0, token: [] as unknown[] };
      const access = createAuthAccess({
        api: fakeApi(calls, { session: boundSession(), accounts }),
        env: ENV,
        app: "storefront",
      });
      await expect(access.getGatewayAccessToken(new Headers())).resolves.toBeNull();
      expect(calls.token).toEqual([]);
    }
  });

  test("Storefront session organization is mandatory and canonical", async () => {
    const calls = { accounts: 0, token: [] as unknown[] };
    const access = createAuthAccess({
      api: fakeApi(calls, { session: boundSession(null) }),
      env: ENV,
      app: "storefront",
    });
    await expect(access.getGatewayAccessToken(new Headers())).resolves.toBeNull();
    expect(calls.accounts).toBe(0);

    const canonical = createAuthAccess({
      api: fakeApi(
        { accounts: 0, token: [] },
        {
          session: boundSession(ORG.toUpperCase()),
          accounts: [linkedAccount()],
        },
      ),
      env: ENV,
      app: "storefront",
    });
    await expect(canonical.getGatewayAccessToken(new Headers())).resolves.toBe(accessToken());
  });

  test("the candidate token must match the session organization", async () => {
    const access = createAuthAccess({
      api: fakeApi(
        { accounts: 0, token: [] },
        {
          session: boundSession(),
          accounts: [linkedAccount()],
          token: accessToken({ organization: { alpha: { id: OTHER_ORG } } }),
        },
      ),
      env: ENV,
      app: "storefront",
    });
    await expect(access.getGatewayAccessToken(new Headers())).resolves.toBeNull();
  });

  test("issuer and subject are narrowed before the token is used", async () => {
    for (const claims of [
      { iss: "https://other.example.test/realms/teck", sub: "subject-1" },
      { iss: ISSUER, sub: "other-subject" },
      { iss: undefined, sub: "subject-1" },
    ]) {
      const access = createAuthAccess({
        api: fakeApi(
          { accounts: 0, token: [] },
          {
            session: boundSession(),
            accounts: [linkedAccount()],
            token: accessToken(claims),
          },
        ),
        env: ENV,
        app: "storefront",
      });
      await expect(access.getGatewayAccessToken(new Headers())).resolves.toBeNull();
    }
  });

  test("uses only the server-side auth row id as the account selector", async () => {
    const calls = { accounts: 0, token: [] as unknown[] };
    const access = createAuthAccess({
      api: fakeApi(calls, { session: boundSession(), accounts: [linkedAccount()] }),
      env: ENV,
      app: "storefront",
    });
    await expect(access.getGatewayAccessToken(new Headers())).resolves.toBe(accessToken());
    expect(calls.token).toEqual([{ accountId: "account-row-1" }]);
  });

  test("API errors require sign-in while store failures are unavailable", async () => {
    const signInRequired = createAuthAccess({
      api: fakeApi(
        { accounts: 0, token: [] },
        {
          session: boundSession(),
          accounts: [linkedAccount()],
          tokenError: APIError.from("BAD_REQUEST", {
            code: "FAILED_TO_GET_ACCESS_TOKEN",
            message: "expired",
          }),
        },
      ),
      env: ENV,
      app: "storefront",
    });
    await expect(signInRequired.getGatewayAccessToken(new Headers())).resolves.toBeNull();

    const unavailable = createAuthAccess({
      api: fakeApi({ accounts: 0, token: [] }, { sessionError: new Error("pool failed") }),
      env: ENV,
      app: "storefront",
    });
    await expect(unavailable.getGatewayAccessToken(new Headers())).rejects.toBeInstanceOf(
      AuthUnavailableError,
    );
  });

  test("checkout identity carries the exact HMAC scope and current bearer", async () => {
    const access = createAuthAccess({
      api: fakeApi(
        { accounts: 0, token: [] },
        { session: boundSession(), accounts: [linkedAccount()] },
      ),
      env: ENV,
      app: "storefront",
    });
    const identity = await access.getCommerceIdentity(new Headers());
    const expectedScope = createHmac("sha256", SECRET)
      .update(JSON.stringify(["checkout-intents:v1", ISSUER, "subject-1", ORG]))
      .digest("base64url");
    expect(identity).toEqual({ accessToken: accessToken(), scope: expectedScope });
  });

  test("session scope is deterministic, opaque and bound to the session", async () => {
    const make = (sessionId: string) =>
      createAuthAccess({
        api: fakeApi(
          { accounts: 0, token: [] },
          {
            session: {
              session: { id: sessionId, userId: "user-1", commerceOrganizationId: ORG },
              user: { id: "user-1" },
            },
            accounts: [linkedAccount()],
          },
        ),
        env: ENV,
        app: "storefront",
      });
    const first = await make("session-1").appSessionScope(new Headers());
    const firstAgain = await make("session-1").appSessionScope(new Headers());
    const second = await make("session-2").appSessionScope(new Headers());

    expect(first).toBeTruthy();
    expect(firstAgain).toBe(first as string);
    expect(second).not.toBe(first as string);
    expect(first as string).not.toContain("user-1");
    expect(first as string).not.toContain("session-1");
  });
});

describe("claim and request narrowing", () => {
  test("rich single-organization UUID claim only", () => {
    const cases: [unknown, string | null][] = [
      [{ alpha: { id: ORG } }, ORG],
      [{ alpha: { id: ORG.toUpperCase() } }, ORG],
      [undefined, null],
      [{}, null],
      [{ alpha: {} }, null],
      [{ alpha: { id: "not-a-uuid" } }, null],
      [{ alpha: { id: ORG }, beta: { id: OTHER_ORG } }, null],
      [["alpha"], null],
      ["alpha", null],
    ];
    for (const [organization, expected] of cases) {
      expect(singleOrganizationId({ organization })).toBe(expected);
    }
    expect(canonicalOrganizationId(` ${ORG.toUpperCase()} `)).toBe(ORG);
    expect(canonicalOrganizationId("alias")).toBeNull();
  });

  test("JWT payload decoding rejects malformed and oversized tokens", () => {
    expect(readJwtClaims(accessToken())?.sub).toBe("subject-1");
    for (const token of ["", "a.b", "a.not-json.c", "a.b.c.d", "x".repeat(20_000)]) {
      expect(readJwtClaims(token)).toBeNull();
    }
  });

  test("organization aliases are a single bounded hint", () => {
    expect(parseOrganizationAlias("alpha.team_1-x")).toBe("alpha.team_1-x");
    for (const value of ["", " ", "two words", "a/b", "a".repeat(129), null, 3]) {
      expect(parseOrganizationAlias(value)).toBeNull();
    }
  });

  test("return URLs stay relative to this app", () => {
    expect(safeReturnTo("/orders?cursor=abc", ENV.baseURL)).toBe("/orders?cursor=abc");
    for (const value of [
      null,
      "",
      "//evil.example",
      "https://evil.example/orders",
      "/\\evil.example",
      "javascript:alert(1)",
      "/line\nbreak",
    ]) {
      expect(safeReturnTo(value, ENV.baseURL)).toBe("/");
    }
  });

  test("actions require the configured origin", () => {
    const same = new Headers({ origin: ENV.baseURL });
    expect(() => assertSameOrigin(same, ENV.baseURL)).not.toThrow();
    const fetchMetadata = new Headers({ "sec-fetch-site": "same-origin" });
    expect(() => assertSameOrigin(fetchMetadata, ENV.baseURL)).not.toThrow();
    for (const headers of [
      new Headers({ origin: "https://evil.example" }),
      new Headers(),
      new Headers({ "sec-fetch-site": "cross-site" }),
    ]) {
      expect(() => assertSameOrigin(headers, ENV.baseURL)).toThrow(CrossOriginActionError);
    }
  });

  test("session organization derives only from the single linked account", async () => {
    const decrypt = async (token: string) => token;
    await expect(
      deriveSessionOrganization([linkedAccount({ accessToken: accessToken() })], decrypt, ISSUER),
    ).resolves.toBe(ORG);
    await expect(deriveSessionOrganization([], decrypt, ISSUER)).resolves.toBeNull();
    await expect(
      deriveSessionOrganization([linkedAccount(), linkedAccount({ id: "two" })], decrypt, ISSUER),
    ).resolves.toBeNull();
    await expect(
      deriveSessionOrganization([linkedAccount({ accessToken: null })], decrypt, ISSUER),
    ).resolves.toBeNull();
    await expect(
      deriveSessionOrganization(
        [
          linkedAccount({
            accessToken: accessToken({ organization: { alpha: { id: OTHER_ORG } } }),
          }),
        ],
        async (token) => token,
        ISSUER,
      ),
    ).resolves.toBe(OTHER_ORG);
    await expect(
      deriveSessionOrganization(
        [linkedAccount()],
        async () => {
          throw new Error("decrypt failed");
        },
        ISSUER,
      ),
    ).resolves.toBeNull();
    await expect(
      deriveSessionOrganization(
        [linkedAccount({ accessToken: accessToken({ iss: "https://other.test" }) })],
        decrypt,
        ISSUER,
      ),
    ).resolves.toBeNull();
  });
});

describe("sign-in", () => {
  const baseRequest = {
    app: "storefront" as const,
    baseURL: ENV.baseURL,
    headers: new Headers({ origin: ENV.baseURL }),
    returnTo: "/orders?cursor=abc",
  };

  test("Storefront adds one validated organization scope", () => {
    expect(buildSignInScopes("storefront", "alpha.team_1-x")).toEqual([
      "openid",
      "profile",
      "email",
      "organization:alpha.team_1-x",
    ]);
    expect(buildSignInScopes("admin", "ignored")).toEqual(["openid", "profile", "email"]);
    for (const value of ["", "two words", "a/b", "a".repeat(129)]) {
      expect(() => buildSignInScopes("storefront", value)).toThrow(SignInInputError);
    }
  });

  test("starts Keycloak with same-app callback and no tenant header", async () => {
    const calls: unknown[] = [];
    const url = await performSignIn({
      ...baseRequest,
      organization: "alpha",
      api: {
        async signInSocial(input) {
          calls.push(input.body);
          return { url: "https://identity.example.test/authorize" };
        },
      },
    });
    expect(url).toBe("https://identity.example.test/authorize");
    expect(calls).toEqual([
      {
        provider: "keycloak",
        callbackURL: "/orders?cursor=abc",
        errorCallbackURL: "/sign-in?error=sign-in",
        scopes: ["openid", "profile", "email", "organization:alpha"],
      },
    ]);
  });

  test("invalid alias or foreign origin sends no provider request", async () => {
    let called = false;
    const api = {
      async signInSocial() {
        called = true;
        return { url: "https://identity.example.test/authorize" };
      },
    };
    await expect(
      performSignIn({ ...baseRequest, organization: "bad alias", api }),
    ).rejects.toBeInstanceOf(SignInInputError);
    await expect(
      performSignIn({
        ...baseRequest,
        headers: new Headers({ origin: "https://evil.example" }),
        organization: "alpha",
        api,
      }),
    ).rejects.toBeInstanceOf(CrossOriginActionError);
    expect(called).toBe(false);
  });

  test("absolute return URLs are replaced before reaching Keycloak", async () => {
    let callbackURL = "";
    await performSignIn({
      ...baseRequest,
      returnTo: "https://evil.example/orders",
      organization: "alpha",
      api: {
        async signInSocial(input) {
          callbackURL = input.body.callbackURL;
          return { url: "https://identity.example.test/authorize" };
        },
      },
    });
    expect(callbackURL).toBe("/");
  });
});

describe("configuration", () => {
  test("missing or unsafe env fails closed without echoing values", () => {
    const valid: Record<string, string> = {
      BETTER_AUTH_URL: ENV.baseURL,
      BETTER_AUTH_SECRET: SECRET,
      AUTH_DATABASE_URL: ENV.databaseURL,
      KEYCLOAK_ISSUER: ENV.issuer,
      KEYCLOAK_CLIENT_ID: ENV.clientId,
      KEYCLOAK_CLIENT_SECRET: ENV.clientSecret,
      COMMERCE_GATEWAY_URL: ENV.gatewayURL,
    };
    expect(readAuthEnv(valid, "storefront").issuer).toBe(ISSUER);
    for (const name of Object.keys(valid)) {
      const missing = { ...valid };
      delete missing[name];
      expect(() => readAuthEnv(missing, "storefront")).toThrow(AuthConfigError);
    }
    expect(() => readAuthEnv({ ...valid, BETTER_AUTH_SECRET: "short" }, "storefront")).toThrow(
      AuthConfigError,
    );
    expect(() =>
      readAuthEnv({ ...valid, AUTH_DATABASE_URL: "mysql://db/auth" }, "storefront"),
    ).toThrow(AuthConfigError);
    expect(() =>
      readAuthEnv({ ...valid, BETTER_AUTH_URL: "http://storefront.example.test" }, "storefront"),
    ).toThrow(AuthConfigError);
    expect(() =>
      readAuthEnv({ ...valid, BETTER_AUTH_URL: "http://127.0.0.1:3000" }, "storefront"),
    ).not.toThrow();
    expect(
      readAuthEnv({ ...valid, BETTER_AUTH_URL: "http://127.0.0.1:3000" }, "storefront").baseURL,
    ).toBe("http://127.0.0.1:3000");
  });

  test("Better Auth options disable passwords, linking, account cookies and cache", async () => {
    const options = buildAuthOptions(ENV, "storefront");
    expect(options.emailAndPassword?.enabled).toBe(false);
    expect(options.account?.encryptOAuthTokens).toBe(true);
    expect(options.account?.storeAccountCookie).toBe(false);
    expect(options.account?.accountLinking).toEqual({
      enabled: false,
      disableImplicitLinking: true,
    });
    expect(options.session?.cookieCache).toEqual({ enabled: false });
    expect(options.session?.additionalFields).toBe(STOREFRONT_SESSION_FIELDS);
    expect(STOREFRONT_SESSION_FIELDS.commerceOrganizationId.input).toBe(false);
    expect(options.trustedOrigins).toEqual([ENV.baseURL]);
    expect(options.advanced?.cookiePrefix).toBe("teck-storefront");
    expect(options.advanced?.crossSubDomainCookies).toEqual({ enabled: false });
    expect(options.advanced?.defaultCookieAttributes).toMatchObject({
      httpOnly: true,
      sameSite: "lax",
      secure: true,
      path: "/",
    });
    expect(options.telemetry).toEqual({ enabled: false });

    const plugins = options.plugins as unknown as {
      id?: string;
      options?: { config?: { requireIdTokenVerification?: boolean; pkce?: boolean }[] };
    }[];
    expect(plugins.map((plugin) => plugin.id)).toEqual(["generic-oauth", "next-cookies"]);
    expect(plugins[0]?.options?.config?.[0]?.requireIdTokenVerification).toBe(true);
    expect(plugins[0]?.options?.config?.[0]?.pkce).toBe(true);

    const pool = options.database as Pool;
    await pool.end();
  });
});
